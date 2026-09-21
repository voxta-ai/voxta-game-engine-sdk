using System;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using Voxta.Unity.Protocol.Generated;
using Voxta.Unity.Transport;

namespace Voxta.Unity
{
    /// <summary>Captures Unity microphone audio and streams PCM16 frames when the server requests recording.</summary>
    [DisallowMultipleComponent]
    public sealed class VoxtaMicrophone : MonoBehaviour
    {
        private const float DefaultSilenceRmsFloor = 12f / 32768f;
        [SerializeField] private string deviceName = "";
        [SerializeField] [Min(8000)] private int requestedSampleRate = 16000;
        [SerializeField] [Range(10, 100)] private int frameMilliseconds = 30;
        [SerializeField] private bool substituteSilence = true;
        [SerializeField] [Min(0)] private float silenceRmsFloor = DefaultSilenceRmsFloor;
        [SerializeField] [Min(0)] private int silenceHangoverMilliseconds = 200;

        private readonly ConcurrentQueue<OutboundFrame> outbound = new ConcurrentQueue<OutboundFrame>();
        private CancellationTokenSource cancellation;
        private ClientWebSocket socket;
        private AudioClip captureClip;
        private float[] samples;
        private int lastReadPosition;
        private int frameSamples;
        private int channels;
        private int hangoverFramesRemaining;
        private int sending;
        private bool opening;
        private bool streaming;
        private VoxtaClient client;
        private VoxtaChatSession session;
        private Uri serverUri;
        private string accessToken;

        public event Action CaptureStarted;
        public event Action CaptureStopped;
        public event Action<ServerSpeechRecognitionStartMessage> RecognitionStarted;
        public event Action<ServerSpeechRecognitionPartialMessage> RecognitionPartial;
        public event Action<ServerSpeechRecognitionEndMessage> RecognitionEnded;
        public event Action<ServerAudioFrameMessage> AudioFrameReceived;
        public event Action<Exception> Error;

        /// <summary>True only when this enabled component can open a local microphone stream.</summary>
        public bool IsInputEnabled => enabled && gameObject.activeInHierarchy && Application.platform != RuntimePlatform.WebGLPlayer && Microphone.devices.Length != 0;
        public bool IsStreaming => streaming;

        internal void Bind(VoxtaClient value, VoxtaChatSession chatSession, Uri baseUri, string token)
        {
            Unbind();
            client = value ?? throw new ArgumentNullException(nameof(value));
            session = chatSession ?? throw new ArgumentNullException(nameof(chatSession));
            serverUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
            accessToken = token ?? string.Empty;
            client.MessageReceived += HandleServerMessage;
        }

        internal void Unbind()
        {
            if (client != null) client.MessageReceived -= HandleServerMessage;
            StopStreaming();
            client = null;
            session = null;
            serverUri = null;
            accessToken = null;
        }

        private void OnDisable() => StopStreaming();
        private void OnDestroy() => Unbind();

        private void Update()
        {
            if (!streaming || captureClip == null || socket == null || socket.State != WebSocketState.Open)
                return;

            var position = Microphone.GetPosition(SelectedDevice);
            if (position < 0) return;
            var available = position - lastReadPosition;
            if (available < 0) available += captureClip.samples;
            while (available >= frameSamples)
            {
                ReadFrame(lastReadPosition);
                lastReadPosition = (lastReadPosition + frameSamples) % captureClip.samples;
                available -= frameSamples;
            }
        }

        private void HandleServerMessage(ServerMessage message)
        {
            if (message is ServerRecordingRequestMessage request && session != null && request.SessionId == session.SessionId)
            {
                if (request.Enabled) BeginStreaming(); else StopStreaming();
                return;
            }
            if (message is ServerSpeechRecognitionStartMessage start) RecognitionStarted?.Invoke(start);
            else if (message is ServerSpeechRecognitionPartialMessage partial) RecognitionPartial?.Invoke(partial);
            else if (message is ServerSpeechRecognitionEndMessage end) RecognitionEnded?.Invoke(end);
            else if (message is ServerAudioFrameMessage frame) AudioFrameReceived?.Invoke(frame);
        }

        private async void BeginStreaming()
        {
            if (streaming || opening || !IsInputEnabled || session == null || session.SessionId == Guid.Empty) return;
            opening = true;
            try
            {
                captureClip = Microphone.Start(SelectedDevice, true, 1, requestedSampleRate);
                if (captureClip == null) throw new InvalidOperationException("Unity did not create a microphone capture clip.");
                channels = captureClip.channels;
                frameSamples = Math.Max(1, captureClip.frequency * frameMilliseconds / 1000);
                samples = new float[frameSamples * channels];
                lastReadPosition = Microphone.GetPosition(SelectedDevice);
                if (lastReadPosition < 0) lastReadPosition = 0;
                hangoverFramesRemaining = 0;

                cancellation = new CancellationTokenSource();
                socket = new ClientWebSocket();
                if (!string.IsNullOrWhiteSpace(accessToken)) socket.Options.SetRequestHeader("Authorization", "Bearer " + accessToken);
                var streamUri = VoxtaWebsocketUrl.ToAudioInputStreamUri(serverUri, session.SessionId);
                await socket.ConnectAsync(streamUri, cancellation.Token);
                QueueText("{\"contentType\":\"audio/wav\",\"sampleRate\":" + captureClip.frequency + ",\"channels\":" + channels + ",\"bitsPerSample\":16,\"bufferMilliseconds\":" + frameMilliseconds + "}");
                streaming = true;
                opening = false;
                CaptureStarted?.Invoke();
                _ = ReceiveUntilClosedAsync(socket, cancellation.Token);
            }
            catch (Exception exception)
            {
                UnityMainThreadDispatcher.Post(() =>
                {
                    StopStreaming();
                    Error?.Invoke(exception);
                });
            }
        }

        private string SelectedDevice => string.IsNullOrWhiteSpace(deviceName) ? null : deviceName;

        private void ReadFrame(int position)
        {
            var firstSamples = Math.Min(frameSamples, captureClip.samples - position);
            if (!captureClip.GetData(samples, position))
            {
                HandleSocketFailure(socket, new InvalidOperationException("Unable to read the microphone capture clip."));
                return;
            }
            if (firstSamples < frameSamples)
            {
                var tail = new float[(frameSamples - firstSamples) * channels];
                if (!captureClip.GetData(tail, 0))
                {
                    HandleSocketFailure(socket, new InvalidOperationException("Unable to read the wrapped microphone capture clip."));
                    return;
                }
                Array.Copy(tail, 0, samples, firstSamples * channels, tail.Length);
            }

            var rms = Pcm16Conversion.CalculateRms(samples);
            if (substituteSilence && rms < silenceRmsFloor && hangoverFramesRemaining <= 0)
            {
                QueueText("{\"type\":\"silence\",\"milliseconds\":" + frameMilliseconds + "}");
                return;
            }
            if (rms >= silenceRmsFloor)
                hangoverFramesRemaining = Mathf.CeilToInt(silenceHangoverMilliseconds / (float)frameMilliseconds);
            else if (hangoverFramesRemaining > 0)
                hangoverFramesRemaining--;
            QueueBinary(Pcm16Conversion.ToLittleEndianPcm16(samples));
        }

        private void QueueText(string text) => Queue(new OutboundFrame(Encoding.UTF8.GetBytes(text), WebSocketMessageType.Text));
        private void QueueBinary(byte[] data) => Queue(new OutboundFrame(data, WebSocketMessageType.Binary));
        private void Queue(OutboundFrame frame)
        {
            if (socket == null || socket.State != WebSocketState.Open) return;
            outbound.Enqueue(frame);
            if (Interlocked.Exchange(ref sending, 1) == 0) _ = SendQueuedAsync(socket, cancellation.Token);
        }

        private async Task SendQueuedAsync(ClientWebSocket currentSocket, CancellationToken token)
        {
            try
            {
                while (outbound.TryDequeue(out var frame))
                {
                    if (currentSocket.State != WebSocketState.Open) return;
                    await currentSocket.SendAsync(new ArraySegment<byte>(frame.Data), frame.Type, true, token);
                }
            }
            catch (Exception exception) { HandleSocketFailure(currentSocket, exception); }
            finally
            {
                Interlocked.Exchange(ref sending, 0);
                if (!outbound.IsEmpty && currentSocket.State == WebSocketState.Open && Interlocked.Exchange(ref sending, 1) == 0)
                    _ = SendQueuedAsync(currentSocket, token);
            }
        }

        private async Task ReceiveUntilClosedAsync(ClientWebSocket currentSocket, CancellationToken token)
        {
            try
            {
                var buffer = new byte[256];
                while (currentSocket.State == WebSocketState.Open)
                {
                    var result = await currentSocket.ReceiveAsync(new ArraySegment<byte>(buffer), token);
                    if (result.MessageType == WebSocketMessageType.Close) break;
                }
            }
            catch (OperationCanceledException) { return; }
            catch (Exception exception) { HandleSocketFailure(currentSocket, exception); return; }
            UnityMainThreadDispatcher.Post(() =>
            {
                if (socket == currentSocket) StopStreaming();
            });
        }

        private void HandleSocketFailure(ClientWebSocket currentSocket, Exception exception) => UnityMainThreadDispatcher.Post(() =>
        {
            if (socket == currentSocket) StopStreaming();
            Error?.Invoke(exception);
        });

        private void StopStreaming()
        {
            var wasStreaming = streaming || captureClip != null || socket != null;
            opening = false;
            streaming = false;
            while (outbound.TryDequeue(out _)) { }
            cancellation?.Cancel();
            cancellation?.Dispose();
            cancellation = null;
            socket?.Abort();
            socket?.Dispose();
            socket = null;
            if (captureClip != null && Microphone.IsRecording(SelectedDevice)) Microphone.End(SelectedDevice);
            captureClip = null;
            samples = null;
            if (wasStreaming) CaptureStopped?.Invoke();
        }

        private readonly struct OutboundFrame
        {
            public readonly byte[] Data;
            public readonly WebSocketMessageType Type;
            public OutboundFrame(byte[] data, WebSocketMessageType type) { Data = data; Type = type; }
        }
    }

    internal static class Pcm16Conversion
    {
        internal static byte[] ToLittleEndianPcm16(float[] samples)
        {
            if (samples == null) throw new ArgumentNullException(nameof(samples));
            var result = new byte[samples.Length * 2];
            for (var i = 0; i < samples.Length; i++)
            {
                var value = samples[i] <= -1f ? short.MinValue : samples[i] >= 1f ? short.MaxValue : (short)Mathf.RoundToInt(samples[i] * short.MaxValue);
                result[i * 2] = (byte)value;
                result[i * 2 + 1] = (byte)(value >> 8);
            }
            return result;
        }

        internal static float CalculateRms(float[] samples)
        {
            if (samples == null || samples.Length == 0) return 0;
            double sum = 0;
            for (var i = 0; i < samples.Length; i++) sum += samples[i] * samples[i];
            return (float)Math.Sqrt(sum / samples.Length);
        }
    }
}

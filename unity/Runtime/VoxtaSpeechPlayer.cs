using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Networking;
using Voxta.Unity.Protocol.Generated;

namespace Voxta.Unity
{
    [Serializable]
    public struct VoxtaSpeechPlaybackMetrics
    {
        public Guid MessageId;
        public int StartIndex;
        public int EndIndex;
        public double DurationSeconds;
        public bool IsNarration;
        public string Affect;
    }

    /// <summary>Downloads and plays server reply audio, then acknowledges its actual playback lifecycle.</summary>
    [DisallowMultipleComponent]
    public sealed class VoxtaSpeechPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;

        private readonly Dictionary<Guid, ReplyState> replies = new Dictionary<Guid, ReplyState>();
        private VoxtaClient client;
        private VoxtaChatSession session;
        private Uri serverUri;
        private AudioClip playingClip;
        private float lastPlaybackEnd;

        public event Action<ServerReplyChunkMessage, VoxtaSpeechPlaybackMetrics> SpeechStarted;
        public event Action<Guid> SpeechEnded;
        public event Action<Guid> SpeechInterrupted;
        public event Action<Exception> SpeechFailed;

        public AudioSource AudioSource => audioSource;
        public bool IsPlaying => audioSource != null && audioSource.isPlaying;
        /// <summary>Whether a reply is downloading, waiting for its gap, or playing through this output.</summary>
        public bool HasActiveReply => replies.Count != 0;
        /// <summary>Whether this component is configured to accept URL playback during companion initialization.</summary>
        public bool IsOutputEnabled => enabled && gameObject.activeInHierarchy && audioSource != null && audioSource.enabled;

        internal void Bind(VoxtaClient value, VoxtaChatSession chatSession, Uri baseUri)
        {
            Unbind();
            client = value ?? throw new ArgumentNullException(nameof(value));
            session = chatSession ?? throw new ArgumentNullException(nameof(chatSession));
            serverUri = baseUri ?? throw new ArgumentNullException(nameof(baseUri));
            session.ReplyChunk += HandleReplyChunk;
            session.ReplyCompleted += HandleReplyEnd;
            client.MessageReceived += HandleServerMessage;
        }

        internal void Unbind()
        {
            if (session != null)
            {
                session.ReplyChunk -= HandleReplyChunk;
                session.ReplyCompleted -= HandleReplyEnd;
            }
            if (client != null) client.MessageReceived -= HandleServerMessage;
            StopAllReplies(false);
            client = null;
            session = null;
            serverUri = null;
        }

        /// <summary>Stops local speech and asks the server to interrupt the active reply.</summary>
        public void Interrupt()
        {
            if (client == null || session == null || session.SessionId == Guid.Empty) return;
            client.Send(new ClientInterruptMessage { SessionId = session.SessionId });
            StopAllReplies(true);
        }

        private void Awake()
        {
            if (audioSource == null) audioSource = GetComponent<AudioSource>();
            if (audioSource == null) audioSource = gameObject.AddComponent<AudioSource>();
        }

        private void OnDisable() => StopAllReplies(false);
        private void OnDestroy() => Unbind();

        private void HandleReplyChunk(ServerReplyChunkMessage chunk)
        {
            var reply = GetOrCreateReply(chunk.SessionId, chunk.MessageId);

            var item = new PreparedChunk(chunk);
            reply.Chunks.Enqueue(item);
            StartCoroutine(PrepareChunk(reply, item));
        }

        private void HandleReplyEnd(ServerReplyEndMessage replyEnd)
        {
            GetOrCreateReply(replyEnd.SessionId, replyEnd.MessageId).Ended = true;
        }

        private void HandleServerMessage(ServerMessage message)
        {
            var replyStart = message as ServerReplyStartMessage;
            if (replyStart != null && session != null && replyStart.SessionId == session.SessionId)
            {
                GetOrCreateReply(replyStart.SessionId, replyStart.MessageId);
                return;
            }

            var interrupt = message as ServerInterruptSpeechMessage;
            if (interrupt == null || session == null || interrupt.SessionId != session.SessionId) return;
            StopReply(interrupt.MessageId, true);
        }

        private ReplyState GetOrCreateReply(Guid sessionId, Guid messageId)
        {
            if (replies.TryGetValue(messageId, out var reply)) return reply;
            reply = new ReplyState(sessionId, messageId);
            replies.Add(messageId, reply);
            StartCoroutine(ProcessReply(reply));
            return reply;
        }

        private IEnumerator PrepareChunk(ReplyState reply, PreparedChunk item)
        {
            if (string.IsNullOrEmpty(item.Chunk.AudioUrl))
            {
                item.Ready = true;
                yield break;
            }
            if (!Uri.TryCreate(serverUri, item.Chunk.AudioUrl, out var audioUri))
            {
                item.Error = new InvalidOperationException("The reply audio URL is invalid.");
                item.Ready = true;
                yield break;
            }

            using (var request = UnityWebRequestMultimedia.GetAudioClip(audioUri.AbsoluteUri, AudioType.WAV))
            {
                request.SetRequestHeader("Accept", "audio/x-wav");
                yield return request.SendWebRequest();
                if (!reply.Cancelled && request.result == UnityWebRequest.Result.Success)
                    item.Clip = DownloadHandlerAudioClip.GetContent(request);
                else if (!reply.Cancelled)
                    item.Error = new InvalidOperationException("Unable to download reply audio: " + request.error);
            }
            item.Ready = true;
        }

        private IEnumerator ProcessReply(ReplyState reply)
        {
            while (!reply.Cancelled)
            {
                if (reply.Chunks.Count == 0)
                {
                    if (reply.Ended)
                    {
                        replies.Remove(reply.MessageId);
                        SendPlaybackComplete(reply);
                        SpeechEnded?.Invoke(reply.MessageId);
                        yield break;
                    }
                    yield return null;
                    continue;
                }

                var item = reply.Chunks.Peek();
                if (!item.Ready)
                {
                    yield return null;
                    continue;
                }
                reply.Chunks.Dequeue();
                if (item.Error != null) SpeechFailed?.Invoke(item.Error);
                yield return PlayChunk(reply, item);
            }
        }

        private IEnumerator PlayChunk(ReplyState reply, PreparedChunk item)
        {
            var gap = Mathf.Max(0, item.Chunk.AudioGapMs.GetValueOrDefault()) / 1000f;
            while (!reply.Cancelled && Time.realtimeSinceStartup - lastPlaybackEnd < gap) yield return null;
            if (reply.Cancelled) yield break;

            var metrics = new VoxtaSpeechPlaybackMetrics
            {
                MessageId = item.Chunk.MessageId,
                StartIndex = item.Chunk.StartIndex,
                EndIndex = item.Chunk.EndIndex,
                DurationSeconds = item.Clip == null ? 0d : item.Clip.length,
                IsNarration = item.Chunk.IsNarration,
                Affect = item.Chunk.Affect
            };
            client.Send(new ClientSpeechPlaybackStartMessage
            {
                SessionId = reply.SessionId,
                MessageId = item.Chunk.MessageId,
                StartIndex = item.Chunk.StartIndex,
                EndIndex = item.Chunk.EndIndex,
                Duration = metrics.DurationSeconds,
                IsNarration = item.Chunk.IsNarration
            });
            SpeechStarted?.Invoke(item.Chunk, metrics);

            if (item.Clip != null)
            {
                playingClip = item.Clip;
                audioSource.clip = item.Clip;
                audioSource.Play();
                while (!reply.Cancelled && audioSource.isPlaying) yield return null;
                audioSource.Stop();
                audioSource.clip = null;
                Destroy(item.Clip);
                playingClip = null;
            }
            lastPlaybackEnd = Time.realtimeSinceStartup;
        }

        private void StopAllReplies(bool notify)
        {
            var ids = new List<Guid>(replies.Keys);
            foreach (var id in ids) StopReply(id, notify);
        }

        private void StopReply(Guid messageId, bool notify)
        {
            if (!replies.TryGetValue(messageId, out var reply)) return;
            reply.Cancelled = true;
            replies.Remove(messageId);
            if (audioSource != null) { audioSource.Stop(); audioSource.clip = null; }
            if (playingClip != null) { Destroy(playingClip); playingClip = null; }
            SendPlaybackComplete(reply);
            if (notify) SpeechInterrupted?.Invoke(messageId);
        }

        private void SendPlaybackComplete(ReplyState reply)
        {
            if (client == null) return;
            client.Send(new ClientSpeechPlaybackCompleteMessage { SessionId = reply.SessionId, MessageId = reply.MessageId });
        }

        private sealed class ReplyState
        {
            public readonly Guid SessionId;
            public readonly Guid MessageId;
            public readonly Queue<PreparedChunk> Chunks = new Queue<PreparedChunk>();
            public bool Ended;
            public bool Cancelled;
            public ReplyState(Guid sessionId, Guid messageId) { SessionId = sessionId; MessageId = messageId; }
        }

        private sealed class PreparedChunk
        {
            public readonly ServerReplyChunkMessage Chunk;
            public AudioClip Clip;
            public Exception Error;
            public bool Ready;
            public PreparedChunk(ServerReplyChunkMessage chunk) { Chunk = chunk; }
        }
    }
}

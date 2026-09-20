using System;
using UnityEngine;
using UnityEngine.Events;
using Voxta.Unity.Protocol.Generated;

namespace Voxta.Unity
{
    [Serializable] public sealed class VoxtaConnectionStateUnityEvent : UnityEvent<VoxtaConnectionState> { }
    [Serializable] public sealed class VoxtaWelcomeUnityEvent : UnityEvent<ServerWelcomeMessage> { }
    [Serializable] public sealed class VoxtaChatStartedUnityEvent : UnityEvent<ServerChatStartedMessage> { }
    [Serializable] public sealed class VoxtaReplyChunkUnityEvent : UnityEvent<ServerReplyChunkMessage> { }
    [Serializable] public sealed class VoxtaReplyCompleteUnityEvent : UnityEvent<ServerReplyEndMessage> { }
    [Serializable] public sealed class VoxtaErrorUnityEvent : UnityEvent<string> { }
    [Serializable] public sealed class VoxtaDiagnosticUnityEvent : UnityEvent<string> { }

    /// <summary>Text-only M1 Unity component that owns one Voxta connection and chat session.</summary>
    public sealed class VoxtaCompanion : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] private string serverUrl = "http://127.0.0.1:5384";
        [SerializeField] private string apiKey = "";
        [Header("Chat")]
        [SerializeField] private string characterId = "";
        [SerializeField] private string scenarioId = "";
        [Header("Capabilities")]
        [SerializeField] private AudioInputClientCapabilities audioInput = AudioInputClientCapabilities.None;
        [SerializeField] private AudioOutputClientCapabilities audioOutput = AudioOutputClientCapabilities.None;
        [SerializeField] private VisionCaptureClientCapabilities visionCapture = VisionCaptureClientCapabilities.None;
        [Header("Events")]
        [SerializeField] private VoxtaConnectionStateUnityEvent onConnectionStateChanged = new VoxtaConnectionStateUnityEvent();
        [SerializeField] private VoxtaWelcomeUnityEvent onWelcome = new VoxtaWelcomeUnityEvent();
        [SerializeField] private VoxtaChatStartedUnityEvent onChatStarted = new VoxtaChatStartedUnityEvent();
        [SerializeField] private VoxtaReplyChunkUnityEvent onReplyChunk = new VoxtaReplyChunkUnityEvent();
        [SerializeField] private VoxtaReplyCompleteUnityEvent onReplyComplete = new VoxtaReplyCompleteUnityEvent();
        [SerializeField] private VoxtaErrorUnityEvent onError = new VoxtaErrorUnityEvent();
        [SerializeField] private VoxtaDiagnosticUnityEvent onDiagnostic = new VoxtaDiagnosticUnityEvent();

        private VoxtaClient client;
        private VoxtaChatSession session;

        public event Action<VoxtaConnectionState> ConnectionStateChanged;
        public event Action<ServerWelcomeMessage> WelcomeReceived;
        public event Action<ServerChatStartedMessage> ChatStarted;
        public event Action<ServerReplyChunkMessage> ReplyChunkReceived;
        public event Action<ServerReplyEndMessage> ReplyCompleted;
        public event Action<Exception> Error;
        public event Action<string> Diagnostic;

        public VoxtaConnectionState ConnectionState => client == null ? VoxtaConnectionState.Disconnected : client.State;
        public VoxtaChatSession ChatSession => session;

        private void OnEnable()
        {
            ReportDiagnostic("Companion enabled.");
            Connect();
        }

        private async void OnDisable()
        {
            if (session != null) { session.Dispose(); session = null; }
            if (client != null) { await client.DisposeAsync(); client = null; }
        }

        public async void Connect()
        {
            if (client != null) return;
            try
            {
                if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out var uri)) throw new ArgumentException("Server URL must be an absolute HTTP or WebSocket URL.", nameof(serverUrl));
                ReportDiagnostic("Opening SignalR connection to " + Transport.VoxtaWebsocketUrl.ToHubUri(uri));
                client = new VoxtaClient(uri, apiKey, new ClientCapabilities { AudioInput = audioInput, AudioOutput = audioOutput, VisionCapture = visionCapture });
                session = new VoxtaChatSession(client);
                client.ConnectionStateChanged += HandleConnectionState;
                client.WelcomeReceived += HandleWelcome;
                client.WelcomeReceived += _ => StartConfiguredChat();
                client.Error += HandleError;
                session.Started += HandleChatStarted;
                session.ReplyChunk += HandleReplyChunk;
                session.ReplyCompleted += HandleReplyCompleted;
                await client.ConnectAsync();
            }
            catch (Exception exception) { HandleError(exception); }
        }

        public void SendText(string text) { try { session?.SendText(text); } catch (Exception exception) { HandleError(exception); } }
        public async void Disconnect() { if (client == null) return; try { await client.DisconnectAsync(); } catch (Exception exception) { HandleError(exception); } }
        private void StartConfiguredChat()
        {
            try
            {
                if (!Guid.TryParse(characterId, out var parsedCharacterId)) throw new ArgumentException("Character ID must be a GUID.", nameof(characterId));
                Guid? parsedScenarioId = null;
                if (!string.IsNullOrWhiteSpace(scenarioId))
                {
                    if (!Guid.TryParse(scenarioId, out var value)) throw new ArgumentException("Scenario ID must be empty or a GUID.", nameof(scenarioId));
                    parsedScenarioId = value;
                }
                session.Start(parsedCharacterId, parsedScenarioId);
            }
            catch (Exception exception) { HandleError(exception); }
        }

        private void HandleConnectionState(VoxtaConnectionState value)
        {
            ReportDiagnostic("Connection state: " + value);
            ConnectionStateChanged?.Invoke(value);
            onConnectionStateChanged.Invoke(value);
        }
        private void HandleWelcome(ServerWelcomeMessage value) { ReportDiagnostic("Welcome received from API " + value.ApiVersion); WelcomeReceived?.Invoke(value); onWelcome.Invoke(value); }
        private void HandleChatStarted(ServerChatStartedMessage value) { ReportDiagnostic("Chat started: " + value.ChatId); ChatStarted?.Invoke(value); onChatStarted.Invoke(value); }
        private void HandleReplyChunk(ServerReplyChunkMessage value) { ReplyChunkReceived?.Invoke(value); onReplyChunk.Invoke(value); }
        private void HandleReplyCompleted(ServerReplyEndMessage value) { ReplyCompleted?.Invoke(value); onReplyComplete.Invoke(value); }
        private void HandleError(Exception exception)
        {
            Debug.LogError("Voxta error: " + exception, this);
            Diagnostic?.Invoke("ERROR: " + exception);
            onDiagnostic.Invoke("ERROR: " + exception);
            Error?.Invoke(exception);
            onError.Invoke(exception.Message);
        }

        private void ReportDiagnostic(string message)
        {
            Debug.Log("Voxta " + message, this);
            Diagnostic?.Invoke(message);
            onDiagnostic.Invoke(message);
        }
    }
}

using System;
using UnityEngine;
using UnityEngine.Events;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.Unity
{
    [Serializable] public sealed class VoxtaConnectionStateUnityEvent : UnityEvent<VoxtaConnectionState> { }
    [Serializable] public sealed class VoxtaWelcomeUnityEvent : UnityEvent<ServerWelcomeMessage> { }
    [Serializable] public sealed class VoxtaChatStartedUnityEvent : UnityEvent<ServerChatStartedMessage> { }
    [Serializable] public sealed class VoxtaReplyChunkUnityEvent : UnityEvent<ServerReplyChunkMessage> { }
    [Serializable] public sealed class VoxtaReplyCompleteUnityEvent : UnityEvent<ServerReplyEndMessage> { }
    [Serializable] public sealed class VoxtaSpeechMetricsUnityEvent : UnityEvent<VoxtaSpeechPlaybackMetrics> { }
    [Serializable] public sealed class VoxtaSpeechMessageUnityEvent : UnityEvent<string> { }
    [Serializable] public sealed class VoxtaRecognitionPartialUnityEvent : UnityEvent<string> { }
    [Serializable] public sealed class VoxtaRecognitionEndUnityEvent : UnityEvent<string> { }
    [Serializable] public sealed class VoxtaAudioFrameUnityEvent : UnityEvent<ServerAudioFrameMessage> { }
    [Serializable] public sealed class VoxtaContextUpdatedUnityEvent : UnityEvent<ServerContextUpdatedMessage> { }
    [Serializable] public sealed class VoxtaAnimationPlayUnityEvent : UnityEvent<ServerAnimationPlayMessage> { }
    [Serializable] public sealed class VoxtaActionErrorUnityEvent : UnityEvent<ServerChatSessionErrorMessage> { }
    [Serializable] public sealed class VoxtaErrorUnityEvent : UnityEvent<string> { }
    [Serializable] public sealed class VoxtaDiagnosticUnityEvent : UnityEvent<string> { }

    /// <summary>Owns one Voxta connection, chat session, and optional Unity speech playback path.</summary>
    public sealed class VoxtaCompanion : MonoBehaviour
    {
        [Header("Connection")]
        [SerializeField] private string serverUrl = "http://127.0.0.1:5384";
        [SerializeField] private string apiKey = "";
        [Header("Chat")]
        [SerializeField] private string characterId = "";
        [SerializeField] private string scenarioId = "";
        [Header("Capabilities")]
        [Tooltip("Use the server's local audio output. This disables Unity playback and reports audioOutput: None.")]
        [SerializeField] private bool localServerAudioOutput;
        [SerializeField] private VisionCaptureClientCapabilities visionCapture = VisionCaptureClientCapabilities.None;
        [Header("Events")]
        [SerializeField] private VoxtaConnectionStateUnityEvent onConnectionStateChanged = new VoxtaConnectionStateUnityEvent();
        [SerializeField] private VoxtaWelcomeUnityEvent onWelcome = new VoxtaWelcomeUnityEvent();
        [SerializeField] private VoxtaChatStartedUnityEvent onChatStarted = new VoxtaChatStartedUnityEvent();
        [SerializeField] private VoxtaReplyChunkUnityEvent onReplyChunk = new VoxtaReplyChunkUnityEvent();
        [SerializeField] private VoxtaReplyCompleteUnityEvent onReplyComplete = new VoxtaReplyCompleteUnityEvent();
        [SerializeField] private VoxtaSpeechMetricsUnityEvent onSpeechStart = new VoxtaSpeechMetricsUnityEvent();
        [SerializeField] private VoxtaSpeechMessageUnityEvent onSpeechEnd = new VoxtaSpeechMessageUnityEvent();
        [SerializeField] private VoxtaSpeechMessageUnityEvent onSpeechInterrupted = new VoxtaSpeechMessageUnityEvent();
        [SerializeField] private UnityEvent onRecognitionStarted = new UnityEvent();
        [SerializeField] private VoxtaRecognitionPartialUnityEvent onRecognitionPartial = new VoxtaRecognitionPartialUnityEvent();
        [SerializeField] private VoxtaRecognitionEndUnityEvent onRecognitionEnded = new VoxtaRecognitionEndUnityEvent();
        [SerializeField] private VoxtaAudioFrameUnityEvent onAudioFrame = new VoxtaAudioFrameUnityEvent();
        [SerializeField] private VoxtaContextUpdatedUnityEvent onContextUpdated = new VoxtaContextUpdatedUnityEvent();
        [SerializeField] private VoxtaAnimationPlayUnityEvent onAnimationPlay = new VoxtaAnimationPlayUnityEvent();
        [SerializeField] private VoxtaActionErrorUnityEvent onActionError = new VoxtaActionErrorUnityEvent();
        [SerializeField] private VoxtaErrorUnityEvent onError = new VoxtaErrorUnityEvent();
        [SerializeField] private VoxtaDiagnosticUnityEvent onDiagnostic = new VoxtaDiagnosticUnityEvent();

        private VoxtaClient client;
        private VoxtaChatSession session;
        private VoxtaSpeechPlayer speechPlayer;
        private VoxtaMicrophone microphone;
        private VoxtaActions actions;

        public event Action<VoxtaConnectionState> ConnectionStateChanged;
        public event Action<ServerWelcomeMessage> WelcomeReceived;
        public event Action<ServerChatStartedMessage> ChatStarted;
        public event Action<ServerReplyChunkMessage> ReplyChunkReceived;
        public event Action<ServerReplyEndMessage> ReplyCompleted;
        public event Action<ServerReplyChunkMessage, VoxtaSpeechPlaybackMetrics> SpeechStarted;
        public event Action<Guid> SpeechEnded;
        public event Action<Guid> SpeechInterrupted;
        public event Action RecognitionStarted;
        public event Action<ServerSpeechRecognitionPartialMessage> RecognitionPartialReceived;
        public event Action<ServerSpeechRecognitionEndMessage> RecognitionEnded;
        public event Action<ServerAudioFrameMessage> AudioFrameReceived;
        public event Action<ServerContextUpdatedMessage> ContextUpdated;
        public event Action<ServerAnimationPlayMessage> AnimationPlayReceived;
        public event Action<ServerChatSessionErrorMessage> ActionErrorReceived;
        public event Action<Exception> Error;
        public event Action<string> Diagnostic;

        public VoxtaConnectionState ConnectionState => client == null ? VoxtaConnectionState.Disconnected : client.State;
        public VoxtaChatSession ChatSession => session;
        public VoxtaSpeechPlayer SpeechPlayer => speechPlayer;
        public VoxtaMicrophone Microphone => microphone;
        public VoxtaActions Actions => actions;
        public VoxtaContextUpdatedUnityEvent OnContextUpdated => onContextUpdated;
        public VoxtaAnimationPlayUnityEvent OnAnimationPlay => onAnimationPlay;
        public VoxtaActionErrorUnityEvent OnActionError => onActionError;
        public string ServerUrl => serverUrl;

        private void Awake()
        {
            speechPlayer = GetComponent<VoxtaSpeechPlayer>();
            microphone = GetComponent<VoxtaMicrophone>();
            actions = GetComponent<VoxtaActions>();
        }

        private void OnEnable()
        {
            ReportDiagnostic("Companion enabled.");
            Connect();
        }

        private async void OnDisable()
        {
            if (speechPlayer != null)
            {
                speechPlayer.SpeechStarted -= HandleSpeechStarted;
                speechPlayer.SpeechEnded -= HandleSpeechEnded;
                speechPlayer.SpeechInterrupted -= HandleSpeechInterrupted;
                speechPlayer.Unbind();
            }
            if (microphone != null)
            {
                microphone.CaptureStarted -= HandleCaptureStarted;
                microphone.CaptureStopped -= HandleCaptureStopped;
                microphone.RecognitionStarted -= HandleRecognitionStarted;
                microphone.RecognitionPartial -= HandleRecognitionPartial;
                microphone.RecognitionEnded -= HandleRecognitionEnded;
                microphone.AudioFrameReceived -= HandleAudioFrame;
                microphone.Error -= HandleError;
                microphone.Unbind();
            }
            if (actions != null) actions.Unbind();
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
                if (speechPlayer == null) speechPlayer = GetComponent<VoxtaSpeechPlayer>();
                if (microphone == null) microphone = GetComponent<VoxtaMicrophone>();
                if (actions == null) actions = GetComponent<VoxtaActions>();
                var unityPlaybackActive = speechPlayer != null && speechPlayer.IsOutputEnabled && !localServerAudioOutput;
                var microphoneInputActive = microphone != null && microphone.IsInputEnabled;
                ReportDiagnostic(DescribeAudioOutput(unityPlaybackActive));
                ReportDiagnostic(microphoneInputActive ? "Unity microphone streaming is enabled; advertising audioInput: WebSocketStream." : "Unity microphone streaming is disabled or unavailable; advertising audioInput: None.");
                client = new VoxtaClient(uri, apiKey, new ClientCapabilities
                {
                    AudioInput = microphoneInputActive ? AudioInputClientCapabilities.WebSocketStream : AudioInputClientCapabilities.None,
                    AudioOutput = unityPlaybackActive ? AudioOutputClientCapabilities.Url : AudioOutputClientCapabilities.None,
                    AcceptedAudioContentTypes = new[] { "audio/x-wav" },
                    VisionCapture = visionCapture
                });
                session = new VoxtaChatSession(client);
                if (actions != null) actions.Bind(session);
                if (unityPlaybackActive)
                {
                    speechPlayer.Bind(client, session, uri, apiKey);
                    speechPlayer.SpeechStarted += HandleSpeechStarted;
                    speechPlayer.SpeechEnded += HandleSpeechEnded;
                    speechPlayer.SpeechInterrupted += HandleSpeechInterrupted;
                }
                if (microphoneInputActive)
                {
                    microphone.Bind(client, session, uri, apiKey);
                    microphone.CaptureStarted += HandleCaptureStarted;
                    microphone.CaptureStopped += HandleCaptureStopped;
                    microphone.RecognitionStarted += HandleRecognitionStarted;
                    microphone.RecognitionPartial += HandleRecognitionPartial;
                    microphone.RecognitionEnded += HandleRecognitionEnded;
                    microphone.AudioFrameReceived += HandleAudioFrame;
                    microphone.Error += HandleError;
                }
                client.ConnectionStateChanged += HandleConnectionState;
                client.WelcomeReceived += HandleWelcome;
                client.WelcomeReceived += _ => StartConfiguredChat();
                client.Error += HandleError;
                session.Started += HandleChatStarted;
                session.ReplyChunk += HandleReplyChunk;
                session.ReplyCompleted += HandleReplyCompleted;
                session.ContextUpdated += HandleContextUpdated;
                session.AnimationPlayReceived += HandleAnimationPlay;
                session.ActionErrorReceived += HandleActionError;
                await client.ConnectAsync();
            }
            catch (Exception exception) { HandleError(exception); }
        }

        /// <summary>Interrupts an active Unity reply before submitting the next user turn.</summary>
        public void SendText(string text)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(text) && speechPlayer != null && speechPlayer.HasActiveReply)
                    speechPlayer.Interrupt();
                session?.SendText(text);
            }
            catch (Exception exception) { HandleError(exception); }
        }

        /// <summary>Sets the API key used by the next connection attempt.</summary>
        public void SetApiKey(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("An API key is required.", nameof(value));
            if (client != null) throw new InvalidOperationException("Disconnect before changing the API key.");
            apiKey = value;
        }

        public void InterruptSpeech() { try { speechPlayer?.Interrupt(); } catch (Exception exception) { HandleError(exception); } }
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
        private void HandleSpeechStarted(ServerReplyChunkMessage chunk, VoxtaSpeechPlaybackMetrics metrics) { SpeechStarted?.Invoke(chunk, metrics); onSpeechStart.Invoke(metrics); }
        private void HandleSpeechEnded(Guid messageId) { SpeechEnded?.Invoke(messageId); onSpeechEnd.Invoke(messageId.ToString()); }
        private void HandleSpeechInterrupted(Guid messageId) { SpeechInterrupted?.Invoke(messageId); onSpeechInterrupted.Invoke(messageId.ToString()); }
        private void HandleCaptureStarted() => ReportDiagnostic("Microphone streaming started.");
        private void HandleCaptureStopped() => ReportDiagnostic("Microphone streaming stopped.");
        private void HandleRecognitionStarted(ServerSpeechRecognitionStartMessage _) { RecognitionStarted?.Invoke(); onRecognitionStarted.Invoke(); }
        private void HandleRecognitionPartial(ServerSpeechRecognitionPartialMessage value) { RecognitionPartialReceived?.Invoke(value); onRecognitionPartial.Invoke(value.Text); }
        private void HandleRecognitionEnded(ServerSpeechRecognitionEndMessage value)
        {
            RecognitionEnded?.Invoke(value);
            onRecognitionEnded.Invoke(value.Text ?? string.Empty);
            if (string.IsNullOrWhiteSpace(value.Text)) return;

            try
            {
                // The server sends partials for display, then expects this repaired final transcript
                // to return through the normal chat-message path.
                session?.SendText(value.Text);
            }
            catch (Exception exception) { HandleError(exception); }
        }
        private void HandleAudioFrame(ServerAudioFrameMessage value) { AudioFrameReceived?.Invoke(value); onAudioFrame.Invoke(value); }
        private void HandleContextUpdated(ServerContextUpdatedMessage value) { ContextUpdated?.Invoke(value); onContextUpdated.Invoke(value); }
        private void HandleAnimationPlay(ServerAnimationPlayMessage value) { AnimationPlayReceived?.Invoke(value); onAnimationPlay.Invoke(value); }
        private void HandleActionError(ServerChatSessionErrorMessage value)
        {
            ActionErrorReceived?.Invoke(value);
            onActionError.Invoke(value);
            HandleError(new VoxtaChatSessionException(value));
        }
        private string DescribeAudioOutput(bool unityPlaybackActive)
        {
            if (unityPlaybackActive) return "Unity speech playback is enabled; advertising audioOutput: Url.";
            if (localServerAudioOutput) return "Unity speech playback is disabled by Local Server Audio Output; advertising audioOutput: None for server-side output.";
            if (speechPlayer == null) return "Unity speech playback is disabled because this companion has no enabled VoxtaSpeechPlayer component; advertising audioOutput: None.";
            if (!speechPlayer.enabled || !speechPlayer.gameObject.activeInHierarchy) return "Unity speech playback is disabled because VoxtaSpeechPlayer is disabled or its GameObject is inactive; advertising audioOutput: None.";
            return "Unity speech playback is disabled because VoxtaSpeechPlayer has no enabled AudioSource; advertising audioOutput: None.";
        }
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

    public sealed class VoxtaChatSessionException : Exception
    {
        public ServerChatSessionErrorMessage ServerMessage { get; }

        internal VoxtaChatSessionException(ServerChatSessionErrorMessage serverMessage)
            : base(serverMessage?.Message ?? "Voxta chat session error.")
        {
            ServerMessage = serverMessage ?? throw new ArgumentNullException(nameof(serverMessage));
        }
    }
}

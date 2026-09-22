using System;
using System.Threading;
using UnityEngine;

namespace Voxta.Unity.Samples.BasicIntegration
{
    /// <summary>Renders a minimal text chat panel without requiring a UI prefab.</summary>
    public sealed class BasicIntegrationSample : MonoBehaviour
    {
        [SerializeField] private VoxtaCompanion companion;
        private string input = string.Empty;
        private string transcript = string.Empty;
        private string status = "Preparing device authorization";
        private string diagnostics = string.Empty;
        private Vector2 diagnosticsScrollPosition;
        private VoxtaDeviceCode deviceCode;
        private CancellationTokenSource authorizationCancellation;
        private bool authorizationComplete;

        private void Awake()
        {
            if (companion == null) throw new InvalidOperationException("The Basic Integration sample requires a VoxtaCompanion.");

            status = companion.ConnectionState.ToString();
            companion.ReplyChunkReceived += chunk => transcript += chunk.Text;
            companion.ReplyCompleted += _ => transcript += "\n";
            companion.SpeechStarted += (_, metrics) => AddDiagnostic(
                "Speech started: " + metrics.MessageId + " [" + metrics.StartIndex + ", " + metrics.EndIndex
                + "), " + metrics.DurationSeconds.ToString("F3") + "s");
            companion.SpeechEnded += messageId => AddDiagnostic("Speech completed: " + messageId);
            companion.SpeechInterrupted += messageId => AddDiagnostic("Speech interrupted: " + messageId);
            companion.RecognitionStarted += () => AddDiagnostic("Speech recognition started.");
            companion.RecognitionPartialReceived += value => AddDiagnostic("Speech partial: " + value.Text);
            companion.RecognitionEnded += value =>
            {
                AddDiagnostic("Speech recognition ended: " + (value.Text ?? "(empty)") + " (" + value.Reason + ")");
                if (!string.IsNullOrWhiteSpace(value.Text)) transcript += "You: " + value.Text + "\nVoxta: ";
            };
            companion.AudioFrameReceived += value => AddDiagnostic("Microphone: RMS " + value.Rms.ToString("F4") + ", voice=" + value.VoiceActivity + ", listening=" + value.Listening);
            companion.ConnectionStateChanged += value => { status = value.ToString(); AddDiagnostic("Connection state: " + value); };
            companion.Error += exception => { status = exception.Message; AddDiagnostic("ERROR: " + exception); };
            companion.Diagnostic += AddDiagnostic;
            BeginAuthorization();
        }

        private void OnDisable()
        {
            authorizationCancellation?.Cancel();
            authorizationCancellation?.Dispose();
            authorizationCancellation = null;
        }

        private async void BeginAuthorization()
        {
            authorizationCancellation = new CancellationTokenSource();
            var cancellationToken = authorizationCancellation.Token;
            try
            {
                if (!Uri.TryCreate(companion.ServerUrl, UriKind.Absolute, out var serverUrl))
                    throw new ArgumentException("The companion's Server URL must be an absolute HTTP or HTTPS URL.");

                var authorization = new VoxtaAuth(serverUrl);
                status = "Checking saved authorization";
                AddDiagnostic("Checking the saved device authorization.");
                if (await authorization.ValidateStoredTokenAsync(cancellationToken))
                {
                    AddDiagnostic("Using the saved device authorization.");
                    Connect(authorization.StoredToken);
                    return;
                }

                status = "Requesting device authorization";
                deviceCode = await authorization.RequestDeviceCodeAsync("Unity Basic Integration", cancellationToken);
                status = "Open the verification page and enter the code below";
                AddDiagnostic("Open " + deviceCode.VerificationUrl + " and enter code " + deviceCode.UserCode + ".");
                var token = await authorization.WaitForTokenAsync(deviceCode, cancellationToken);
                AddDiagnostic("Device authorization approved.");
                Connect(token);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                AddDiagnostic("Device authorization cancelled.");
            }
            catch (Exception exception)
            {
                status = exception.Message;
                AddDiagnostic("ERROR: " + exception);
            }
        }

        private void Connect(string token)
        {
            companion.SetApiKey(token);
            deviceCode = null;
            authorizationComplete = true;
            status = "Connecting";
            companion.enabled = true;
        }

        private void OnGUI()
        {
            var panel = new Rect(20, 20, 700, 700);
            GUI.Box(panel, GUIContent.none);
            GUILayout.BeginArea(new Rect(30, 30, 680, 680));
            GUILayout.Label("Voxta Basic Chat — " + DisplayStatus);
            if (authorizationComplete) RenderChat();
            else RenderAuthorization();
            RenderDiagnostics();
            GUILayout.EndArea();
        }

        private string DisplayStatus => authorizationComplete && companion.ConnectionState != VoxtaConnectionState.Faulted
            ? companion.ConnectionState.ToString()
            : status;

        private void RenderAuthorization()
        {
            if (deviceCode != null)
            {
                GUILayout.Label("Verification URL: " + deviceCode.VerificationUrl);
                GUILayout.Label("Code: " + deviceCode.UserCode);
                if (GUILayout.Button("Open Verification Page")) Application.OpenURL(deviceCode.VerificationUrl.AbsoluteUri);
            }
            else
            {
                GUILayout.Label("Waiting for device authorization.");
            }
        }

        private void RenderChat()
        {
            GUILayout.TextArea(transcript, GUILayout.Height(300));
            input = GUILayout.TextField(input);
            if (GUILayout.Button("Send")) Send();
        }

        private void RenderDiagnostics()
        {
            GUILayout.Label("Diagnostics");
            diagnosticsScrollPosition = GUILayout.BeginScrollView(diagnosticsScrollPosition, GUILayout.Height(180));
            GUILayout.TextArea(diagnostics, GUILayout.ExpandHeight(true));
            GUILayout.EndScrollView();
        }

        public void Send()
        {
            if (string.IsNullOrWhiteSpace(input)) return;
            transcript += "You: " + input + "\nVoxta: ";
            companion.SendText(input);
            input = string.Empty;
        }

        private void AddDiagnostic(string message)
        {
            diagnostics += "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + message + "\n";
            diagnosticsScrollPosition.y = float.MaxValue;
        }
    }
}

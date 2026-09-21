using System;
using UnityEngine;

namespace Voxta.Unity.Samples.BasicIntegration
{
    /// <summary>Renders a minimal text chat panel without requiring a UI prefab.</summary>
    public sealed class BasicIntegrationSample : MonoBehaviour
    {
        [SerializeField] private VoxtaCompanion companion;
        private string input = string.Empty;
        private string transcript = string.Empty;
        private string status = "Connecting";
        private string diagnostics = string.Empty;

        private void Awake()
        {
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
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(20, 20, 700, 700));
            GUILayout.Label("Voxta Basic Chat — " + status);
            GUILayout.TextArea(transcript, GUILayout.Height(300));
            input = GUILayout.TextField(input);
            if (GUILayout.Button("Send")) Send();
            GUILayout.Label("Diagnostics");
            GUILayout.TextArea(diagnostics, GUILayout.Height(180));
            GUILayout.EndArea();
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
        }
    }
}

using System;
using Voxta.Unity.Protocol.Generated;

namespace Voxta.Unity
{
    /// <summary>The M1 text-only chat lifecycle for one server chat session.</summary>
    public sealed class VoxtaChatSession : IDisposable
    {
        private readonly VoxtaClient client;
        public Guid SessionId { get; private set; }
        public Guid ChatId { get; private set; }
        public event Action<ServerChatStartedMessage> Started;
        public event Action<ServerReplyChunkMessage> ReplyChunk;
        public event Action<ServerReplyEndMessage> ReplyCompleted;

        public VoxtaChatSession(VoxtaClient client)
        {
            this.client = client ?? throw new ArgumentNullException(nameof(client));
            client.MessageReceived += HandleMessage;
        }

        public void Start(Guid characterId, Guid? scenarioId = null)
        {
            client.Send(new ClientStartChatMessage { CharacterId = characterId, ScenarioId = scenarioId });
        }

        public void SendText(string text)
        {
            if (SessionId == Guid.Empty) throw new InvalidOperationException("The chat session has not started.");
            if (string.IsNullOrWhiteSpace(text)) throw new ArgumentException("Text is required.", nameof(text));
            client.Send(new ClientSendMessage { SessionId = SessionId, Text = text });
        }

        public void Dispose() => client.MessageReceived -= HandleMessage;

        private void HandleMessage(ServerMessage message)
        {
            if (message is ServerChatStartedMessage started)
            {
                SessionId = started.SessionId;
                ChatId = started.ChatId;
                Started?.Invoke(started);
            }
            else if (message is ServerReplyChunkMessage chunk && chunk.SessionId == SessionId) ReplyChunk?.Invoke(chunk);
            else if (message is ServerReplyEndMessage end && end.SessionId == SessionId) ReplyCompleted?.Invoke(end);
        }
    }
}

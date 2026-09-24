using System;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;

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
        public event Action<ServerActionMessage> ActionReceived;
        public event Action<ServerActionAppTriggerMessage> AppTriggerReceived;
        public event Action<ServerContextUpdatedMessage> ContextUpdated;
        public event Action<ServerAnimationPlayMessage> AnimationPlayReceived;
        public event Action<ServerChatSessionErrorMessage> ActionErrorReceived;

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

        /// <summary>Triggers a registered non-tool action against an existing chat message.</summary>
        public void TriggerAction(Guid messageId, string actionName, ActionInvocationArgument[] arguments = null)
        {
            if (SessionId == Guid.Empty) throw new InvalidOperationException("The chat session has not started.");
            if (messageId == Guid.Empty) throw new ArgumentException("A target message ID is required.", nameof(messageId));
            if (string.IsNullOrWhiteSpace(actionName)) throw new ArgumentException("An action name is required.", nameof(actionName));
            client.Send(new ClientTriggerActionMessage { SessionId = SessionId, MessageId = messageId, Value = actionName, Arguments = arguments });
        }

        internal void SendContextUpdate(ClientUpdateContextMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            if (SessionId == Guid.Empty) throw new InvalidOperationException("The chat session has not started.");
            if (message.SessionId != SessionId) throw new ArgumentException("The context update belongs to another chat session.", nameof(message));
            client.Send(message);
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
            else if (message is ServerContextUpdatedMessage contextUpdated && contextUpdated.SessionId == SessionId) ContextUpdated?.Invoke(contextUpdated);
            else if (message is ServerAnimationPlayMessage animationPlay && animationPlay.SessionId == SessionId) AnimationPlayReceived?.Invoke(animationPlay);
            else if (message is ServerChatSessionErrorMessage actionError && actionError.SessionId == SessionId) ActionErrorReceived?.Invoke(actionError);
            else if (message is ServerActionMessage action && action.SessionId == SessionId) ActionReceived?.Invoke(action);
            else if (message is ServerActionAppTriggerMessage appTrigger && appTrigger.SessionId == SessionId)
            {
                AppTriggerReceived?.Invoke(appTrigger);
                if (appTrigger.TriggerId.HasValue)
                    client.Send(new ClientAppTriggerCompleteMessage { SessionId = SessionId, TriggerId = appTrigger.TriggerId.Value });
            }
        }
    }
}

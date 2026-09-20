using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using Voxta.Unity.Protocol.Generated;
using Voxta.Unity.Transport;

namespace Voxta.Unity.Tests
{
    public sealed class VoxtaClientAndChatSessionTests
    {
        [SetUp]
        public void SetUp() => UnityMainThreadDispatcher.DrainPending();

        [Test]
        public void ConnectWelcomeReconnectAndDisconnectFollowExpectedStates()
        {
            var transport = new FakeTransport();
            var client = new VoxtaClient(transport);
            var states = new List<VoxtaConnectionState>();
            client.ConnectionStateChanged += states.Add;

            client.ConnectAsync().GetAwaiter().GetResult();
            UnityMainThreadDispatcher.DrainPending();
            transport.Receive(new ServerWelcomeMessage { ApiVersion = "2025-11" });
            UnityMainThreadDispatcher.DrainPending();
            transport.RaiseReconnecting();
            UnityMainThreadDispatcher.DrainPending();
            transport.RaiseReconnected();
            UnityMainThreadDispatcher.DrainPending();
            client.DisconnectAsync().GetAwaiter().GetResult();
            UnityMainThreadDispatcher.DrainPending();

            CollectionAssert.AreEqual(new[] {
                VoxtaConnectionState.Connecting, VoxtaConnectionState.Authenticating,
                VoxtaConnectionState.Connected, VoxtaConnectionState.Reconnecting,
                VoxtaConnectionState.Authenticating, VoxtaConnectionState.Disconnected
            }, states);
            Assert.That(transport.ConnectCalls, Is.EqualTo(1));
            Assert.That(transport.DisconnectCalls, Is.EqualTo(1));
        }

        [Test]
        public void IncompatibleWelcomeRaisesErrorAndDoesNotPublishWelcome()
        {
            var transport = new FakeTransport();
            var client = new VoxtaClient(transport);
            Exception error = null;
            var welcomed = false;
            client.Error += exception => error = exception;
            client.WelcomeReceived += _ => welcomed = true;

            transport.Receive(new ServerWelcomeMessage { ApiVersion = "2025-12" });
            UnityMainThreadDispatcher.DrainPending();

            Assert.That(error, Is.TypeOf<NotSupportedException>());
            Assert.That(welcomed, Is.False);
            Assert.That(client.State, Is.EqualTo(VoxtaConnectionState.Faulted));
        }

        [Test]
        public void TransportErrorIsDeliveredOnTheDispatcherAndFaultsTheClient()
        {
            var transport = new FakeTransport();
            var client = new VoxtaClient(transport);
            Exception error = null;
            client.Error += exception => error = exception;

            transport.RaiseError(new InvalidOperationException("network failure"));
            Assert.That(error, Is.Null);
            UnityMainThreadDispatcher.DrainPending();

            Assert.That(error, Is.TypeOf<InvalidOperationException>());
            Assert.That(client.State, Is.EqualTo(VoxtaConnectionState.Faulted));
        }

        [Test]
        public void ChatSessionCorrelatesStartChunksAndEndAndSendsTypedMessages()
        {
            var transport = new FakeTransport();
            var client = new VoxtaClient(transport);
            var session = new VoxtaChatSession(client);
            var sessionId = Guid.NewGuid();
            var chatId = Guid.NewGuid();
            var chunks = new List<string>();
            var completed = false;
            session.ReplyChunk += chunk => chunks.Add(chunk.Text);
            session.ReplyCompleted += _ => completed = true;

            session.Start(Guid.NewGuid(), Guid.NewGuid());
            transport.Receive(new ServerChatStartedMessage { SessionId = sessionId, ChatId = chatId });
            transport.Receive(new ServerReplyChunkMessage { SessionId = Guid.NewGuid(), Text = "ignored" });
            transport.Receive(new ServerReplyChunkMessage { SessionId = sessionId, Text = "hello" });
            transport.Receive(new ServerReplyEndMessage { SessionId = sessionId });
            UnityMainThreadDispatcher.DrainPending();
            session.SendText("reply");

            Assert.That(session.SessionId, Is.EqualTo(sessionId));
            Assert.That(session.ChatId, Is.EqualTo(chatId));
            CollectionAssert.AreEqual(new[] { "hello" }, chunks);
            Assert.That(completed, Is.True);
            Assert.That(transport.Sent[0], Is.TypeOf<ClientStartChatMessage>());
            var sent = (ClientSendMessage)transport.Sent[1];
            Assert.That(sent.SessionId, Is.EqualTo(sessionId));
            Assert.That(sent.Text, Is.EqualTo("reply"));
        }

        private sealed class FakeTransport : IVoxtaTransport
        {
            public event Action<ServerMessage> MessageReceived;
            public event Action<Exception> Error;
            public event Action Disconnected;
            public event Action Reconnecting;
            public event Action Reconnected;
            public readonly List<ClientMessage> Sent = new List<ClientMessage>();
            public int ConnectCalls { get; private set; }
            public int DisconnectCalls { get; private set; }

            public Task ConnectAsync(CancellationToken cancellationToken) { ConnectCalls++; return Task.CompletedTask; }
            public Task DisconnectAsync() { DisconnectCalls++; Disconnected?.Invoke(); return Task.CompletedTask; }
            public void Send(ClientMessage message) => Sent.Add(message);
            public ValueTask DisposeAsync() => new ValueTask(DisconnectAsync());
            public void Receive(ServerMessage message) => UnityMainThreadDispatcher.Post(() => MessageReceived?.Invoke(message));
            public void RaiseReconnecting() => UnityMainThreadDispatcher.Post(() => Reconnecting?.Invoke());
            public void RaiseReconnected() => UnityMainThreadDispatcher.Post(() => Reconnected?.Invoke());
            public void RaiseError(Exception exception) => UnityMainThreadDispatcher.Post(() => Error?.Invoke(exception));
        }
    }
}

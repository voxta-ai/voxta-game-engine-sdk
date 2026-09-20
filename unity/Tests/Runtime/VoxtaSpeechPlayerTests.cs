using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Voxta.Unity.Protocol.Generated;
using Voxta.Unity.Transport;

namespace Voxta.Unity.Tests
{
    public sealed class VoxtaSpeechPlayerTests
    {
        [SetUp]
        public void SetUp() => UnityMainThreadDispatcher.DrainPending();

        [UnityTest]
        public IEnumerator ReplyWithoutAudioAcknowledgesStartThenNaturalCompletion()
        {
            var fixture = new PlaybackFixture();
            var messageId = Guid.NewGuid();
            var started = new List<VoxtaSpeechPlaybackMetrics>();
            var ended = new List<Guid>();
            fixture.Player.SpeechStarted += (_, metrics) => started.Add(metrics);
            fixture.Player.SpeechEnded += ended.Add;

            fixture.ReceiveReplyChunk(messageId);
            fixture.ReceiveReplyEnd(messageId);
            yield return null;
            yield return null;

            Assert.That(started, Has.Count.EqualTo(1));
            Assert.That(started[0].MessageId, Is.EqualTo(messageId));
            Assert.That(started[0].DurationSeconds, Is.Zero);
            CollectionAssert.AreEqual(new[] { messageId }, ended);
            Assert.That(fixture.SentOfType<ClientSpeechPlaybackStartMessage>(), Has.Count.EqualTo(1));
            Assert.That(fixture.SentOfType<ClientSpeechPlaybackCompleteMessage>(), Has.Count.EqualTo(1));
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator ReplyWithoutChunksAcknowledgesCompletion()
        {
            var fixture = new PlaybackFixture();
            var messageId = Guid.NewGuid();

            fixture.ReceiveReplyStart(messageId);
            fixture.ReceiveReplyEnd(messageId);
            yield return null;
            yield return null;

            Assert.That(fixture.SentOfType<ClientSpeechPlaybackStartMessage>(), Is.Empty);
            Assert.That(fixture.SentOfType<ClientSpeechPlaybackCompleteMessage>(), Has.Count.EqualTo(1));
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator ServerInterruptionStopsReplyAndAcknowledgesCompletion()
        {
            var fixture = new PlaybackFixture();
            var messageId = Guid.NewGuid();
            var interrupted = new List<Guid>();
            fixture.Player.SpeechInterrupted += interrupted.Add;

            fixture.ReceiveReplyChunk(messageId);
            yield return null;
            Assert.That(fixture.Player.HasActiveReply, Is.True);
            fixture.Transport.Receive(new ServerInterruptSpeechMessage { SessionId = fixture.SessionId, MessageId = messageId });
            UnityMainThreadDispatcher.DrainPending();
            yield return null;

            CollectionAssert.AreEqual(new[] { messageId }, interrupted);
            Assert.That(fixture.Player.HasActiveReply, Is.False);
            Assert.That(fixture.SentOfType<ClientSpeechPlaybackStartMessage>(), Has.Count.EqualTo(1));
            Assert.That(fixture.SentOfType<ClientSpeechPlaybackCompleteMessage>(), Has.Count.EqualTo(1));
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator ServerInterruptionBeforeFirstChunkAcknowledgesCompletion()
        {
            var fixture = new PlaybackFixture();
            var messageId = Guid.NewGuid();

            fixture.ReceiveReplyStart(messageId);
            fixture.Transport.Receive(new ServerInterruptSpeechMessage { SessionId = fixture.SessionId, MessageId = messageId });
            UnityMainThreadDispatcher.DrainPending();
            yield return null;

            Assert.That(fixture.SentOfType<ClientSpeechPlaybackStartMessage>(), Is.Empty);
            Assert.That(fixture.SentOfType<ClientSpeechPlaybackCompleteMessage>(), Has.Count.EqualTo(1));
            fixture.Dispose();
        }

        [UnityTest]
        public IEnumerator PlayerInterruptionSendsInterruptAndAcknowledgesCompletion()
        {
            var fixture = new PlaybackFixture();
            var messageId = Guid.NewGuid();
            var interrupted = new List<Guid>();
            fixture.Player.SpeechInterrupted += interrupted.Add;

            fixture.ReceiveReplyChunk(messageId);
            yield return null;
            Assert.That(fixture.Player.HasActiveReply, Is.True);
            fixture.Player.Interrupt();
            yield return null;

            CollectionAssert.AreEqual(new[] { messageId }, interrupted);
            Assert.That(fixture.Player.HasActiveReply, Is.False);
            Assert.That(fixture.SentOfType<ClientInterruptMessage>(), Has.Count.EqualTo(1));
            Assert.That(fixture.SentOfType<ClientSpeechPlaybackCompleteMessage>(), Has.Count.EqualTo(1));
            fixture.Dispose();
        }

        private sealed class PlaybackFixture : IDisposable
        {
            public readonly FakeTransport Transport = new FakeTransport();
            public readonly Guid SessionId = Guid.NewGuid();
            public readonly VoxtaClient Client;
            public readonly VoxtaChatSession Session;
            public readonly VoxtaSpeechPlayer Player;

            public PlaybackFixture()
            {
                Client = new VoxtaClient(Transport);
                Session = new VoxtaChatSession(Client);
                Transport.Receive(new ServerChatStartedMessage { SessionId = SessionId, ChatId = Guid.NewGuid() });
                UnityMainThreadDispatcher.DrainPending();

                var gameObject = new GameObject("VoxtaSpeechPlayerTests");
                gameObject.AddComponent<AudioSource>();
                Player = gameObject.AddComponent<VoxtaSpeechPlayer>();
                Player.Bind(Client, Session, new Uri("http://localhost:5384/"));
            }

            public void ReceiveReplyChunk(Guid messageId)
            {
                Transport.Receive(new ServerReplyChunkMessage
                {
                    SessionId = SessionId,
                    MessageId = messageId,
                    StartIndex = 0,
                    EndIndex = 1,
                    Text = "test"
                });
                UnityMainThreadDispatcher.DrainPending();
            }

            public void ReceiveReplyStart(Guid messageId)
            {
                Transport.Receive(new ServerReplyStartMessage { SessionId = SessionId, MessageId = messageId });
                UnityMainThreadDispatcher.DrainPending();
            }

            public void ReceiveReplyEnd(Guid messageId)
            {
                Transport.Receive(new ServerReplyEndMessage { SessionId = SessionId, MessageId = messageId });
                UnityMainThreadDispatcher.DrainPending();
            }

            public List<T> SentOfType<T>() where T : ClientMessage
            {
                var messages = new List<T>();
                foreach (var message in Transport.Sent)
                    if (message is T typed) messages.Add(typed);
                return messages;
            }

            public void Dispose()
            {
                Player.Unbind();
                UnityEngine.Object.DestroyImmediate(Player.gameObject);
                Session.Dispose();
            }
        }

        private sealed class FakeTransport : IVoxtaTransport
        {
            public event Action<ServerMessage> MessageReceived;
            public event Action<Exception> Error { add { } remove { } }
            public event Action Disconnected;
            public event Action Reconnecting { add { } remove { } }
            public event Action Reconnected { add { } remove { } }
            public readonly List<ClientMessage> Sent = new List<ClientMessage>();

            public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task DisconnectAsync() { Disconnected?.Invoke(); return Task.CompletedTask; }
            public void Send(ClientMessage message) => Sent.Add(message);
            public ValueTask DisposeAsync() => new ValueTask(DisconnectAsync());
            public void Receive(ServerMessage message) => UnityMainThreadDispatcher.Post(() => MessageReceived?.Invoke(message));
        }
    }
}

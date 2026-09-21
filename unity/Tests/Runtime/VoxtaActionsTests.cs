using System;
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
    public sealed class VoxtaActionsTests
    {
        [SetUp] public void SetUp() => UnityMainThreadDispatcher.DrainPending();

        [Test]
        public void PublishesRegisteredActionsAfterTheChatStarts()
        {
            var transport = new FakeTransport();
            var client = new VoxtaClient(transport);
            var session = new VoxtaChatSession(client);
            var gameObject = new GameObject("VoxtaActionsTests");
            try
            {
                var actions = gameObject.AddComponent<VoxtaActions>();
                actions.SetActions(new[] { new VoxtaActionDefinition("wave", "Wave at the player.", new VoxtaActionArgument("hand", FunctionArgumentType.String, required: true)) });
                actions.Bind(session);
                var sessionId = Guid.NewGuid();

                transport.Receive(new ServerChatStartedMessage { SessionId = sessionId, ChatId = Guid.NewGuid() });
                UnityMainThreadDispatcher.DrainPending();

                Assert.That(transport.Sent, Has.Count.EqualTo(1));
                var message = transport.Sent[0] as ClientUpdateContextMessage;
                Assert.That(message, Is.Not.Null);
                Assert.That(message.SessionId, Is.EqualTo(sessionId));
                Assert.That(message.ContextKey, Is.EqualTo("Unity"));
                Assert.That(message.Actions[0].Name, Is.EqualTo("wave"));
                Assert.That(message.Actions[0].Arguments[0].Required, Is.True);
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        [UnityTest, Explicit("Requires a running local server and the device-flow token saved by the M4 authorization verification.")]
        public System.Collections.IEnumerator PublishesActionRegistrationToTheLocalServer()
        {
            var token = new PlayerPrefsTokenStore().LoadToken();
            if (string.IsNullOrWhiteSpace(token)) Assert.Ignore("No locally authorized device-flow token is available.");

            var client = new VoxtaClient(new Uri("http://127.0.0.1:5384"), token);
            var session = new VoxtaChatSession(client);
            var gameObject = new GameObject("VoxtaActionsLiveTests");
            Exception error = null;
            try
            {
                var actions = gameObject.AddComponent<VoxtaActions>();
                actions.SetActions(new[] { new VoxtaActionDefinition("unity_m4_registration_probe", "A local SDK registration probe.") });
                actions.Bind(session);
                client.Error += value => error = value;
                client.ConnectAsync();

                var timeout = Time.realtimeSinceStartup + 15f;
                while (client.State != VoxtaConnectionState.Connected && error == null && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(error, Is.Null);
                Assert.That(client.State, Is.EqualTo(VoxtaConnectionState.Connected));

                session.Start(new Guid("46ee0414-7610-6011-deec-8533d4c50d90"));
                timeout = Time.realtimeSinceStartup + 15f;
                while (session.SessionId == Guid.Empty && error == null && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(error, Is.Null);
                Assert.That(session.SessionId, Is.Not.EqualTo(Guid.Empty));

                // updateContext has no response message; reaching chatStarted and remaining connected after publication is the server-side acceptance evidence.
                yield return new WaitForSecondsRealtime(1f);
                Assert.That(error, Is.Null);
                Assert.That(client.State, Is.EqualTo(VoxtaConnectionState.Connected));
            }
            finally
            {
                session.Dispose();
                client.DisposeAsync().AsTask().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        private sealed class FakeTransport : IVoxtaTransport
        {
            public event Action<ServerMessage> MessageReceived;
            public event Action<Exception> Error;
            public event Action Disconnected;
            public event Action Reconnecting;
            public event Action Reconnected;
            public readonly List<ClientMessage> Sent = new List<ClientMessage>();
            public Task ConnectAsync(CancellationToken cancellationToken) => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public void Send(ClientMessage message) => Sent.Add(message);
            public ValueTask DisposeAsync() => default;
            public void Receive(ServerMessage message) => UnityMainThreadDispatcher.Post(() => MessageReceived?.Invoke(message));
        }
    }
}

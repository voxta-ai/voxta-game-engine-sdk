using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;
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

        [Test]
        public void DispatchesMatchingActionsToRegisteredHandlersAndUnityEventsOnTheMainThread()
        {
            var transport = new FakeTransport();
            var client = new VoxtaClient(transport);
            var session = new VoxtaChatSession(client);
            var gameObject = new GameObject("VoxtaActionsDispatchTests");
            try
            {
                var actions = gameObject.AddComponent<VoxtaActions>();
                var definition = new VoxtaActionDefinition("wave", "Wave at the player.");
                actions.SetActions(new[] { definition });
                actions.Bind(session);
                ServerActionMessage handled = null;
                ServerActionMessage unityEventAction = null;
                ServerActionMessage definitionUnityEventAction = null;
                var callbackThread = -1;
                actions.RegisterHandler("wave", action => { handled = action; callbackThread = Thread.CurrentThread.ManagedThreadId; });
                actions.OnAction.AddListener(action => unityEventAction = action);
                definition.OnInvoked.AddListener(action => definitionUnityEventAction = action);
                var sessionId = Guid.NewGuid();

                transport.Receive(new ServerChatStartedMessage { SessionId = sessionId, ChatId = Guid.NewGuid() });
                transport.Receive(new ServerActionMessage
                {
                    SessionId = sessionId,
                    ContextKey = "Unity",
                    Value = "wave",
                    Arguments = new[] { new ActionInvocationArgument { Name = "hand", Value = "left" } }
                });

                Assert.That(handled, Is.Null);
                Assert.That(unityEventAction, Is.Null);
                UnityMainThreadDispatcher.DrainPending();

                Assert.That(handled.Value, Is.EqualTo("wave"));
                Assert.That(handled.Arguments[0].Value, Is.EqualTo("left"));
                Assert.That(unityEventAction, Is.SameAs(handled));
                Assert.That(definitionUnityEventAction, Is.SameAs(handled));
                Assert.That(callbackThread, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void PublishesScenarioContextsAndTriggersActionsForTheActiveChat()
        {
            var transport = new FakeTransport();
            var client = new VoxtaClient(transport);
            var session = new VoxtaChatSession(client);
            var gameObject = new GameObject("VoxtaActionsContextTests");
            try
            {
                var actions = gameObject.AddComponent<VoxtaActions>();
                actions.SetContexts(new[] { new VoxtaContextDefinition("The bridge is raised.", "world") });
                actions.Bind(session);
                var sessionId = Guid.NewGuid();
                var messageId = Guid.NewGuid();

                transport.Receive(new ServerChatStartedMessage { SessionId = sessionId, ChatId = Guid.NewGuid() });
                UnityMainThreadDispatcher.DrainPending();
                session.TriggerAction(messageId, "lower_bridge", new[] { new ActionInvocationArgument { Name = "speed", Value = "fast" } });

                var context = transport.Sent[0] as ClientUpdateContextMessage;
                var trigger = transport.Sent[1] as ClientTriggerActionMessage;
                Assert.That(context.Contexts, Has.Length.EqualTo(1));
                Assert.That(context.Contexts[0].Name, Is.EqualTo("world"));
                Assert.That(context.Contexts[0].Text, Is.EqualTo("The bridge is raised."));
                Assert.That(trigger.SessionId, Is.EqualTo(sessionId));
                Assert.That(trigger.MessageId, Is.EqualTo(messageId));
                Assert.That(trigger.Value, Is.EqualTo("lower_bridge"));
                Assert.That(trigger.Arguments[0].Value, Is.EqualTo("fast"));
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        [Test]
        public void DispatchesQueuedAppTriggersThenAcknowledgesThemOnTheMainThread()
        {
            var transport = new FakeTransport();
            var client = new VoxtaClient(transport);
            var session = new VoxtaChatSession(client);
            var sessionId = Guid.NewGuid();
            var triggerId = Guid.NewGuid();
            ServerActionAppTriggerMessage received = null;
            var callbackThread = -1;
            try
            {
                session.AppTriggerReceived += trigger =>
                {
                    received = trigger;
                    callbackThread = Thread.CurrentThread.ManagedThreadId;
                    Assert.That(transport.Sent, Has.None.TypeOf<ClientAppTriggerCompleteMessage>());
                };

                transport.Receive(new ServerChatStartedMessage { SessionId = sessionId, ChatId = Guid.NewGuid() });
                transport.Receive(new ServerActionAppTriggerMessage
                {
                    SessionId = sessionId,
                    TriggerId = triggerId,
                    Name = "setMood",
                    Arguments = new object[] { JsonSerializer.Deserialize<JsonElement>("\"happy\"") }
                });

                UnityMainThreadDispatcher.DrainPending();

                Assert.That(received.Name, Is.EqualTo("setMood"));
                Assert.That(((System.Text.Json.JsonElement)received.Arguments[0]).GetString(), Is.EqualTo("happy"));
                Assert.That(callbackThread, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
                var completion = transport.Sent.Find(message => message is ClientAppTriggerCompleteMessage) as ClientAppTriggerCompleteMessage;
                Assert.That(completion, Is.Not.Null);
                Assert.That(completion.SessionId, Is.EqualTo(sessionId));
                Assert.That(completion.TriggerId, Is.EqualTo(triggerId));
            }
            finally { session.Dispose(); }
        }

        [Test]
        public void DoesNotAcknowledgeUnqueuedOrOtherSessionAppTriggers()
        {
            var transport = new FakeTransport();
            var client = new VoxtaClient(transport);
            var session = new VoxtaChatSession(client);
            var sessionId = Guid.NewGuid();
            var dispatchCount = 0;
            try
            {
                session.AppTriggerReceived += _ => dispatchCount++;
                transport.Receive(new ServerChatStartedMessage { SessionId = sessionId, ChatId = Guid.NewGuid() });
                transport.Receive(new ServerActionAppTriggerMessage { SessionId = sessionId, Name = "fireAndForget" });
                transport.Receive(new ServerActionAppTriggerMessage { SessionId = Guid.NewGuid(), TriggerId = Guid.NewGuid(), Name = "otherSession" });

                UnityMainThreadDispatcher.DrainPending();

                Assert.That(dispatchCount, Is.EqualTo(1));
                Assert.That(transport.Sent, Has.None.TypeOf<ClientAppTriggerCompleteMessage>());
            }
            finally { session.Dispose(); }
        }

        [Test]
        public void DispatchesCompanionMessagesForTheActiveSessionOnTheMainThread()
        {
            var transport = new FakeTransport();
            var client = new VoxtaClient(transport);
            var session = new VoxtaChatSession(client);
            var sessionId = Guid.NewGuid();
            ServerContextUpdatedMessage context = null;
            ServerAnimationPlayMessage animation = null;
            ServerChatSessionErrorMessage error = null;
            var callbackThread = -1;
            try
            {
                session.ContextUpdated += value => { context = value; callbackThread = Thread.CurrentThread.ManagedThreadId; };
                session.AnimationPlayReceived += value => animation = value;
                session.ActionErrorReceived += value => error = value;
                transport.Receive(new ServerChatStartedMessage { SessionId = sessionId, ChatId = Guid.NewGuid() });
                transport.Receive(new ServerContextUpdatedMessage { SessionId = sessionId, Flags = Array.Empty<FlagInfo>(), Characters = Array.Empty<ChatParticipantInfo>(), Roles = new Dictionary<string, ServerContextUpdatedMessage.RoleEntry>() });
                transport.Receive(new ServerAnimationPlayMessage { SessionId = sessionId, AnimationId = Guid.NewGuid(), Url = "/animation", ContentType = "application/json" });
                transport.Receive(new ServerChatSessionErrorMessage { SessionId = sessionId, Message = "Action failed.", Retry = false });
                transport.Receive(new ServerAnimationPlayMessage { SessionId = Guid.NewGuid(), AnimationId = Guid.NewGuid(), Url = "/other", ContentType = "application/json" });

                UnityMainThreadDispatcher.DrainPending();

                Assert.That(context, Is.Not.Null);
                Assert.That(animation, Is.Not.Null);
                Assert.That(error.Message, Is.EqualTo("Action failed."));
                Assert.That(error.Retry, Is.False);
                Assert.That(callbackThread, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));
            }
            finally { session.Dispose(); }
        }

        [Test]
        public void CompanionExposesTypedEventsAndUnityEventsForCompanionMessages()
        {
            var gameObject = new GameObject("VoxtaCompanionMessageEvents");
            gameObject.SetActive(false);
            try
            {
                var companion = gameObject.AddComponent<VoxtaCompanion>();
                ServerContextUpdatedMessage contextReceived = null;
                ServerAnimationPlayMessage animationReceived = null;
                ServerChatSessionErrorMessage errorReceived = null;
                ServerContextUpdatedMessage contextUnityEvent = null;
                ServerAnimationPlayMessage animationUnityEvent = null;
                ServerChatSessionErrorMessage errorUnityEvent = null;
                companion.ContextUpdated += value => contextReceived = value;
                companion.AnimationPlayReceived += value => animationReceived = value;
                companion.ActionErrorReceived += value => errorReceived = value;
                companion.OnContextUpdated.AddListener(value => contextUnityEvent = value);
                companion.OnAnimationPlay.AddListener(value => animationUnityEvent = value);
                companion.OnActionError.AddListener(value => errorUnityEvent = value);
                var context = new ServerContextUpdatedMessage { Flags = Array.Empty<FlagInfo>(), Characters = Array.Empty<ChatParticipantInfo>(), Roles = new Dictionary<string, ServerContextUpdatedMessage.RoleEntry>() };
                var animation = new ServerAnimationPlayMessage { AnimationId = Guid.NewGuid(), Url = "/animation", ContentType = "application/json" };
                var error = new ServerChatSessionErrorMessage { Message = "Action failed." };

                InvokeCompanionHandler(companion, "HandleContextUpdated", context);
                InvokeCompanionHandler(companion, "HandleAnimationPlay", animation);
                LogAssert.Expect(LogType.Error, new Regex("Voxta error:.*Action failed\\."));
                InvokeCompanionHandler(companion, "HandleActionError", error);

                Assert.That(contextUnityEvent, Is.SameAs(contextReceived));
                Assert.That(animationUnityEvent, Is.SameAs(animationReceived));
                Assert.That(errorUnityEvent, Is.SameAs(errorReceived));
            }
            finally { UnityEngine.Object.DestroyImmediate(gameObject); }
        }

        private static void InvokeCompanionHandler(VoxtaCompanion companion, string name, object message)
        {
            var handler = typeof(VoxtaCompanion).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(handler, Is.Not.Null);
            handler.Invoke(companion, new[] { message });
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

        [UnityTest, Explicit("Requires a running local server, the device-flow token saved by the M4 authorization verification, and a character that permits inferred actions.")]
        public System.Collections.IEnumerator DispatchesAnActionFromTheLocalServer()
        {
            var token = new PlayerPrefsTokenStore().LoadToken();
            if (string.IsNullOrWhiteSpace(token)) Assert.Ignore("No locally authorized device-flow token is available.");

            var client = new VoxtaClient(new Uri("http://127.0.0.1:5384"), token);
            var session = new VoxtaChatSession(client);
            var gameObject = new GameObject("VoxtaActionsLiveDispatchTests");
            Exception error = null;
            ServerActionMessage receivedByClient = null;
            ServerActionMessage received = null;
            try
            {
                var actions = gameObject.AddComponent<VoxtaActions>();
                actions.RegisterHandler("unity_m4_dispatch_probe", action => received = action);
                actions.Bind(session);
                client.Error += value => error = value;
                client.MessageReceived += message =>
                {
                    if (message is ServerActionMessage action) receivedByClient = action;
                };
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

                // Avoid adding actions while the server is creating the chat's initial turn.
                yield return new WaitForSecondsRealtime(2f);
                actions.SetActions(new[] { new VoxtaActionDefinition("unity_m4_dispatch_probe", "A local SDK action-dispatch probe.", new VoxtaActionArgument("hand", FunctionArgumentType.String)) });
                // updateContext is queued as chat input, so let the server import the action before requesting it.
                yield return new WaitForSecondsRealtime(1f);
                session.SendText("Call the unity_m4_dispatch_probe action now with hand set to left.");
                // The server can defer client effects until the spoken reply has completed.
                timeout = Time.realtimeSinceStartup + 35f;
                while (receivedByClient == null && error == null && Time.realtimeSinceStartup < timeout) yield return null;

                Assert.That(error, Is.Null);
                Assert.That(receivedByClient, Is.Not.Null, "The server inferred the action but did not send an action frame. An installed chat augmentation may have handled it.");
                Assert.That(received, Is.Not.Null, "VoxtaActions did not dispatch the received action. Check its session and context key.");
                Assert.That(received.ContextKey, Is.EqualTo("Unity"));
                Assert.That(received.Value, Is.EqualTo("unity_m4_dispatch_probe"));
            }
            finally
            {
                session.Dispose();
                client.DisposeAsync().AsTask().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [UnityTest, Explicit("Requires a running local server, the device-flow token saved by the M4 authorization verification, and the local Assistant scenario.")]
        public System.Collections.IEnumerator TriggersARegisteredActionWithScenarioContextOnTheLocalServer()
        {
            var token = new PlayerPrefsTokenStore().LoadToken();
            if (string.IsNullOrWhiteSpace(token)) Assert.Ignore("No locally authorized device-flow token is available.");

            var client = new VoxtaClient(new Uri("http://127.0.0.1:5384"), token);
            var session = new VoxtaChatSession(client);
            var gameObject = new GameObject("VoxtaActionsLiveTriggerTests");
            Exception error = null;
            Guid bootstrapMessageId = Guid.Empty;
            Guid completedBootstrapMessageId = Guid.Empty;
            ServerActionMessage received = null;
            try
            {
                var actions = gameObject.AddComponent<VoxtaActions>();
                actions.RegisterHandler("unity_m4_trigger_context_probe", action => received = action);
                actions.Bind(session);
                client.Error += value => error = value;
                session.ReplyChunk += chunk =>
                {
                    if (bootstrapMessageId == Guid.Empty) bootstrapMessageId = chunk.MessageId;
                };
                session.ReplyCompleted += end =>
                {
                    if (end.MessageId == bootstrapMessageId) completedBootstrapMessageId = end.MessageId;
                };
                client.ConnectAsync();

                var timeout = Time.realtimeSinceStartup + 15f;
                while (client.State != VoxtaConnectionState.Connected && error == null && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(error, Is.Null);
                Assert.That(client.State, Is.EqualTo(VoxtaConnectionState.Connected));

                session.Start(
                    new Guid("46ee0414-7610-6011-deec-8533d4c50d90"),
                    new Guid("8347d016-3562-eb7f-62aa-348cd7befbc5"));
                timeout = Time.realtimeSinceStartup + 20f;
                while (session.SessionId == Guid.Empty && error == null && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(error, Is.Null);
                Assert.That(session.SessionId, Is.Not.EqualTo(Guid.Empty));

                // This scenario's bootstrap reply is persisted; wait for its completion before using its ID as the trigger target.
                timeout = Time.realtimeSinceStartup + 35f;
                while (completedBootstrapMessageId == Guid.Empty && error == null && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(error, Is.Null);
                Assert.That(completedBootstrapMessageId, Is.EqualTo(bootstrapMessageId).And.Not.EqualTo(Guid.Empty));

                actions.SetContexts(new[] { new VoxtaContextDefinition("The local trigger probe context is active.", "trigger-probe") });
                actions.SetActions(new[] { new VoxtaActionDefinition("unity_m4_trigger_context_probe", "A local SDK trigger-action probe.") });
                // updateContext is queued as chat input and has no acknowledgement frame.
                yield return new WaitForSecondsRealtime(1f);
                Assert.That(error, Is.Null);
                Assert.That(client.State, Is.EqualTo(VoxtaConnectionState.Connected));

                session.TriggerAction(completedBootstrapMessageId, "unity_m4_trigger_context_probe");
                timeout = Time.realtimeSinceStartup + 15f;
                while (received == null && error == null && Time.realtimeSinceStartup < timeout) yield return null;

                Assert.That(error, Is.Null);
                Assert.That(received, Is.Not.Null, "The server did not return the directly triggered action to the registered Unity handler.");
                Assert.That(received.ContextKey, Is.EqualTo("Unity"));
                Assert.That(received.Value, Is.EqualTo("unity_m4_trigger_context_probe"));
            }
            finally
            {
                session.Dispose();
                client.DisposeAsync().AsTask().GetAwaiter().GetResult();
                UnityEngine.Object.DestroyImmediate(gameObject);
            }
        }

        [UnityTest, Explicit("Requires a running local server, the device-flow token saved by the M4 authorization verification, and the local queued app-trigger scenario.")]
        public System.Collections.IEnumerator DispatchesAndAcknowledgesAQueuedAppTriggerOnTheLocalServer()
        {
            var token = new PlayerPrefsTokenStore().LoadToken();
            if (string.IsNullOrWhiteSpace(token)) Assert.Ignore("No locally authorized device-flow token is available.");

            var client = new VoxtaClient(new Uri("http://127.0.0.1:5384"), token);
            var session = new VoxtaChatSession(client);
            Exception error = null;
            ServerActionAppTriggerMessage received = null;
            var callbackThread = -1;
            try
            {
                client.Error += value => error = value;
                session.AppTriggerReceived += trigger =>
                {
                    received = trigger;
                    callbackThread = Thread.CurrentThread.ManagedThreadId;
                };
                client.ConnectAsync();

                var timeout = Time.realtimeSinceStartup + 15f;
                while (client.State != VoxtaConnectionState.Connected && error == null && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(error, Is.Null);
                Assert.That(client.State, Is.EqualTo(VoxtaConnectionState.Connected));

                session.Start(
                    new Guid("46ee0414-7610-6011-deec-8533d4c50d90"),
                    new Guid("cad4e287-861c-f35c-b561-3611727cf803"));
                timeout = Time.realtimeSinceStartup + 20f;
                while (session.SessionId == Guid.Empty && error == null && Time.realtimeSinceStartup < timeout) yield return null;
                Assert.That(error, Is.Null);
                Assert.That(session.SessionId, Is.Not.EqualTo(Guid.Empty));

                // The server initializes scenario event handling after chatStarted.
                yield return new WaitForSecondsRealtime(2f);
                session.SendText("Run the Unity M4 app-trigger probe.");
                timeout = Time.realtimeSinceStartup + 20f;
                while (received == null && error == null && Time.realtimeSinceStartup < timeout) yield return null;

                Assert.That(error, Is.Null);
                Assert.That(received, Is.Not.Null, "The scenario did not emit the queued Unity app-trigger probe.");
                Assert.That(received.SessionId, Is.EqualTo(session.SessionId));
                Assert.That(received.TriggerId, Is.Not.Null, "The scenario must use chat.Queue.AppTrigger so the server requires an acknowledgement.");
                Assert.That(received.Name, Is.EqualTo("unity_m4_app_trigger_probe"));
                Assert.That(received.Arguments, Has.Length.EqualTo(3));
                Assert.That(((System.Text.Json.JsonElement)received.Arguments[0]).GetString(), Is.EqualTo("hello"));
                Assert.That(((System.Text.Json.JsonElement)received.Arguments[1]).GetInt32(), Is.EqualTo(7));
                Assert.That(((System.Text.Json.JsonElement)received.Arguments[2]).GetBoolean(), Is.True);
                Assert.That(callbackThread, Is.EqualTo(Thread.CurrentThread.ManagedThreadId));

                // The queued foreground trigger has been acknowledged by the session before this callback returns.
                yield return new WaitForSecondsRealtime(1f);
                Assert.That(error, Is.Null);
                Assert.That(client.State, Is.EqualTo(VoxtaConnectionState.Connected));
            }
            finally
            {
                session.Dispose();
                client.DisposeAsync().AsTask().GetAwaiter().GetResult();
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

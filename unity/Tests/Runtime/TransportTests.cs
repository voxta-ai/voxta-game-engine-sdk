using System;
using System.Reflection;
using System.Text.Json;
using NUnit.Framework;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;
using Voxta.Unity.Transport;

namespace Voxta.Unity.Tests
{
    public sealed class TransportTests
    {
        [TestCase("http://localhost:5384", "ws://localhost:5384/hub")]
        [TestCase("https://example.test/base/", "wss://example.test/base/hub")]
        [TestCase("wss://example.test/base?ignored=true", "wss://example.test/base/hub")]
        public void HubUrlUsesWebSocketSchemeAndCanonicalHubPath(string input, string expected)
        {
            Assert.That(VoxtaWebsocketUrl.ToHubUri(new Uri(input)).AbsoluteUri, Is.EqualTo(expected));
        }

        [Test]
        public void DispatcherDefersCallbacksUntilUnityPump()
        {
            var called = false;
            UnityMainThreadDispatcher.Post(() => called = true);
            Assert.That(called, Is.False);
            UnityMainThreadDispatcher.DrainPending();
            Assert.That(called, Is.True);
        }

        [Test]
        public void AudioInputUrlUsesTheRawStreamEndpointAndSessionQuery()
        {
            var sessionId = Guid.Parse("11111111-2222-3333-4444-555555555555");
            var result = VoxtaWebsocketUrl.ToAudioInputStreamUri(new Uri("https://example.test/base/"), sessionId);
            Assert.That(result.AbsoluteUri, Is.EqualTo("wss://example.test/base/ws/audio/input/stream?sessionId=11111111-2222-3333-4444-555555555555"));
        }

        [Test]
        public void UnknownTransportFrameIsIgnoredAndLaterKnownFramesAreDelivered()
        {
            var transport = new VoxtaSignalRTransport(
                new Uri("http://localhost:5384"),
                null,
                new ClientAuthenticateMessage { Client = "test", ClientVersion = "1.0" });
            var received = 0;
            Exception error = null;
            transport.MessageReceived += _ => received++;
            transport.Error += value => error = value;

            try
            {
                InvokeReceiveMessage(transport, "{\"$type\":\"futureServerFrame\"}");
                UnityMainThreadDispatcher.DrainPending();
                Assert.That(received, Is.Zero);
                Assert.That(error, Is.Null);

                InvokeReceiveMessage(transport, "{\"$type\":\"speechRecognitionStart\"}");
                UnityMainThreadDispatcher.DrainPending();
                Assert.That(received, Is.EqualTo(1));
            }
            finally
            {
                transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        [Test]
        public void MalformedActionFrameReportsAnError()
        {
            var transport = new VoxtaSignalRTransport(
                new Uri("http://localhost:5384"),
                null,
                new ClientAuthenticateMessage { Client = "test", ClientVersion = "1.0" });
            Exception error = null;
            transport.Error += value => error = value;

            try
            {
                InvokeReceiveMessage(transport, "{\"$type\":\"action\",\"sessionId\":false}");
                UnityMainThreadDispatcher.DrainPending();

                Assert.That(error, Is.TypeOf<JsonException>());
            }
            finally
            {
                transport.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
        }

        private static void InvokeReceiveMessage(VoxtaSignalRTransport transport, string json)
        {
            using var document = JsonDocument.Parse(json);
            typeof(VoxtaSignalRTransport)
                .GetMethod("HandleReceiveMessage", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(transport, new object[] { document.RootElement });
        }
    }
}

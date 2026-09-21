using System;
using NUnit.Framework;
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
    }
}

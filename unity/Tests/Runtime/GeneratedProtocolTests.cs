using System;
using System.Text.Json;
using NUnit.Framework;
using Voxta.Unity.Protocol.Generated;

namespace Voxta.Unity.Tests
{
    public sealed class GeneratedProtocolTests
    {
        [Test]
        public void AuthenticateMessageWritesPinnedDiscriminator()
        {
            var json = JsonSerializer.Serialize<ClientMessage>(new ClientAuthenticateMessage
            {
                Client = "Voxta Unity SDK",
                ClientVersion = "0.1.0-pre.1"
            }, VoxtaJson.CreateOptions());

            StringAssert.Contains("\"$type\":\"authenticate\"", json);
            StringAssert.Contains("\"client\":\"Voxta Unity SDK\"", json);
        }

        [Test]
        public void ConcreteAuthenticateMessageWritesPinnedDiscriminator()
        {
            var json = JsonSerializer.Serialize(new ClientAuthenticateMessage
            {
                Client = "Voxta Unity SDK",
                ClientVersion = "0.1.0-pre.1"
            }, VoxtaJson.CreateOptions());

            StringAssert.Contains("\"$type\":\"authenticate\"", json);
        }

        [Test]
        public void SendMessageWritesThePinnedUserRoleValue()
        {
            var json = JsonSerializer.Serialize(new ClientSendMessage
            {
                SessionId = Guid.NewGuid(),
                Text = "Hello",
                Role = ChatMessageRole.User
            }, VoxtaJson.CreateOptions());

            StringAssert.Contains("\"role\":3", json);
        }

        [TestCase("welcome", typeof(ServerWelcomeMessage))]
        [TestCase("authenticationRequired", typeof(ServerAuthenticationRequiredMessage))]
        [TestCase("chatStarted", typeof(ServerChatStartedMessage))]
        [TestCase("replyChunk", typeof(ServerReplyChunkMessage))]
        [TestCase("replyEnd", typeof(ServerReplyEndMessage))]
        public void ServerMessageReadsPinnedDiscriminator(string discriminator, Type expectedType)
        {
            var json = "{\"$type\":\"" + discriminator + "\",\"sessionId\":\"00000000-0000-0000-0000-000000000000\"}";
            var message = JsonSerializer.Deserialize<ServerMessage>(json, VoxtaJson.CreateOptions());

            Assert.That(message, Is.TypeOf(expectedType));
        }
    }
}

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
        public void UnityUrlPlaybackAdvertisesOnlyWav()
        {
            var capabilities = new ClientCapabilities();

            CollectionAssert.AreEqual(new[] { "audio/x-wav" }, capabilities.AcceptedAudioContentTypes);
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

        [Test]
        public void SpeechMessagesWritePinnedDiscriminatorsAndFields()
        {
            var start = JsonSerializer.Serialize<ClientMessage>(new ClientSpeechPlaybackStartMessage
            {
                SessionId = Guid.NewGuid(), MessageId = Guid.NewGuid(), StartIndex = 2, EndIndex = 7,
                Duration = 1.25d, IsNarration = true
            }, VoxtaJson.CreateOptions());
            var interrupt = JsonSerializer.Serialize<ClientMessage>(new ClientInterruptMessage { SessionId = Guid.NewGuid() }, VoxtaJson.CreateOptions());

            StringAssert.Contains("\"$type\":\"speechPlaybackStart\"", start);
            StringAssert.Contains("\"duration\":1.25", start);
            StringAssert.Contains("\"isNarration\":true", start);
            StringAssert.Contains("\"$type\":\"interrupt\"", interrupt);
        }

        [TestCase("welcome", typeof(ServerWelcomeMessage))]
        [TestCase("authenticationRequired", typeof(ServerAuthenticationRequiredMessage))]
        [TestCase("chatStarted", typeof(ServerChatStartedMessage))]
        [TestCase("replyStart", typeof(ServerReplyStartMessage))]
        [TestCase("replyChunk", typeof(ServerReplyChunkMessage))]
        [TestCase("replyEnd", typeof(ServerReplyEndMessage))]
        [TestCase("speechPlaybackStart", typeof(ServerSpeechPlaybackStartMessage))]
        [TestCase("speechPlaybackComplete", typeof(ServerSpeechPlaybackCompleteMessage))]
        [TestCase("interruptSpeech", typeof(ServerInterruptSpeechMessage))]
        [TestCase("recordingRequest", typeof(ServerRecordingRequestMessage))]
        [TestCase("speechRecognitionStart", typeof(ServerSpeechRecognitionStartMessage))]
        [TestCase("speechRecognitionPartial", typeof(ServerSpeechRecognitionPartialMessage))]
        [TestCase("speechRecognitionEnd", typeof(ServerSpeechRecognitionEndMessage))]
        [TestCase("audioFrame", typeof(ServerAudioFrameMessage))]
        public void ServerMessageReadsPinnedDiscriminator(string discriminator, Type expectedType)
        {
            var json = "{\"$type\":\"" + discriminator + "\",\"sessionId\":\"00000000-0000-0000-0000-000000000000\"}";
            var message = JsonSerializer.Deserialize<ServerMessage>(json, VoxtaJson.CreateOptions());

            Assert.That(message, Is.TypeOf(expectedType));
        }

        [Test]
        public void AudioFrameAndRecognitionFieldsReadFromPinnedContracts()
        {
            var audio = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"audioFrame\",\"rms\":0.25,\"voiceActivity\":true,\"listening\":true,\"noiseFloorRms\":0.1,\"thresholdRms\":0.2,\"runHeldOpen\":false}", VoxtaJson.CreateOptions()) as ServerAudioFrameMessage;
            var end = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"speechRecognitionEnd\",\"text\":\"hello\",\"reason\":\"EndOfSpeech\",\"words\":[{\"text\":\"hello\",\"confidence\":0.9}]}", VoxtaJson.CreateOptions()) as ServerSpeechRecognitionEndMessage;

            Assert.That(audio.VoiceActivity, Is.True);
            Assert.That(audio.ThresholdRms, Is.EqualTo(0.2f));
            Assert.That(end.Text, Is.EqualTo("hello"));
            Assert.That(end.Words[0].Confidence, Is.EqualTo(0.9d));
        }
    }
}

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

        [Test]
        public void UpdateContextWritesPinnedActionRegistrationContract()
        {
            var json = JsonSerializer.Serialize<ClientMessage>(new ClientUpdateContextMessage
            {
                SessionId = Guid.NewGuid(),
                ContextKey = "Unity",
                Actions = new[]
                {
                    new ScenarioActionDefinition
                    {
                        Name = "wave",
                        Description = "Wave at the player.",
                        Arguments = new[] { new FunctionArgumentDefinition { Name = "hand", Type = FunctionArgumentType.String, Required = true } }
                    }
                }
            }, VoxtaJson.CreateOptions());

            StringAssert.Contains("\"$type\":\"updateContext\"", json);
            StringAssert.Contains("\"contextKey\":\"Unity\"", json);
            StringAssert.Contains("\"name\":\"wave\"", json);
            StringAssert.Contains("\"required\":true", json);
        }

        [Test]
        public void TriggerActionAndContextUpdateWritePinnedContracts()
        {
            var trigger = JsonSerializer.Serialize<ClientMessage>(new ClientTriggerActionMessage
            {
                SessionId = Guid.NewGuid(),
                MessageId = Guid.NewGuid(),
                Value = "wave",
                Arguments = new[] { new ActionInvocationArgument { Name = "hand", Value = "left" } }
            }, VoxtaJson.CreateOptions());
            var context = JsonSerializer.Serialize<ClientMessage>(new ClientUpdateContextMessage
            {
                SessionId = Guid.NewGuid(),
                ContextKey = "Unity",
                Contexts = new[] { new ContextDefinition { Name = "world", Text = "The bridge is raised.", ApplyTo = PromptCategories.Replies, Position = PromptPositions.System } }
            }, VoxtaJson.CreateOptions());

            StringAssert.Contains("\"$type\":\"triggerAction\"", trigger);
            StringAssert.Contains("\"messageId\":", trigger);
            StringAssert.Contains("\"value\":\"wave\"", trigger);
            StringAssert.Contains("\"$type\":\"updateContext\"", context);
            StringAssert.Contains("\"contexts\":[", context);
            StringAssert.Contains("\"applyTo\":1", context);
            StringAssert.Contains("\"position\":1", context);
        }

        [Test]
        public void AppTriggerCompletionWritesPinnedContract()
        {
            var triggerId = Guid.NewGuid();
            var completion = JsonSerializer.Serialize<ClientMessage>(new ClientAppTriggerCompleteMessage
            {
                SessionId = Guid.NewGuid(),
                TriggerId = triggerId
            }, VoxtaJson.CreateOptions());

            StringAssert.Contains("\"$type\":\"appTriggerComplete\"", completion);
            StringAssert.Contains("\"triggerId\":\"" + triggerId + "\"", completion);
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
        [TestCase("contextUpdated", typeof(ServerContextUpdatedMessage))]
        [TestCase("animationPlay", typeof(ServerAnimationPlayMessage))]
        [TestCase("chatSessionError", typeof(ServerChatSessionErrorMessage))]
        [TestCase("action", typeof(ServerActionMessage))]
        [TestCase("appTrigger", typeof(ServerActionAppTriggerMessage))]
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

        [Test]
        public void ActionReadsPinnedFieldsAndStringArguments()
        {
            var message = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"action\",\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"contextKey\":\"Unity\",\"layer\":\"game\",\"value\":\"wave\",\"role\":\"Assistant\",\"senderId\":\"00000000-0000-0000-0000-000000000002\",\"scenarioRole\":\"Guide\",\"arguments\":[{\"name\":\"hand\",\"value\":\"left\"}]} ", VoxtaJson.CreateOptions()) as ServerActionMessage;

            Assert.That(message.ContextKey, Is.EqualTo("Unity"));
            Assert.That(message.Layer, Is.EqualTo("game"));
            Assert.That(message.Value, Is.EqualTo("wave"));
            Assert.That(message.Role, Is.EqualTo(ChatMessageRole.Assistant));
            Assert.That(message.Arguments[0].Name, Is.EqualTo("hand"));
            Assert.That(message.Arguments[0].Value, Is.EqualTo("left"));
        }

        [Test]
        public void AppTriggerReadsPinnedFieldsAndArbitraryArguments()
        {
            var message = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"appTrigger\",\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"messageId\":\"00000000-0000-0000-0000-000000000002\",\"triggerId\":\"00000000-0000-0000-0000-000000000003\",\"name\":\"setMood\",\"arguments\":[\"happy\",3,true],\"senderId\":\"00000000-0000-0000-0000-000000000004\",\"scenarioRole\":\"Guide\"}", VoxtaJson.CreateOptions()) as ServerActionAppTriggerMessage;

            Assert.That(message.MessageId, Is.EqualTo(new Guid("00000000-0000-0000-0000-000000000002")));
            Assert.That(message.TriggerId, Is.EqualTo(new Guid("00000000-0000-0000-0000-000000000003")));
            Assert.That(message.Name, Is.EqualTo("setMood"));
            Assert.That(message.Arguments[0].GetString(), Is.EqualTo("happy"));
            Assert.That(message.Arguments[1].GetInt32(), Is.EqualTo(3));
            Assert.That(message.Arguments[2].GetBoolean(), Is.True);
        }

        [Test]
        public void CompanionMessagesReadPinnedFields()
        {
            var context = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"contextUpdated\",\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"flags\":[{\"name\":\"ready\",\"messageChatTime\":12}],\"variables\":{\"score\":7},\"contexts\":[{\"contextKey\":\"Unity\",\"text\":\"A bridge is raised.\"}],\"actions\":[{\"contextKey\":\"Unity\",\"name\":\"lower_bridge\"}],\"characters\":[],\"roles\":{\"Guide\":{\"enabled\":true}}}", VoxtaJson.CreateOptions()) as ServerContextUpdatedMessage;
            var animation = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"animationPlay\",\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"animationId\":\"00000000-0000-0000-0000-000000000002\",\"url\":\"/api/animations/2\",\"contentType\":\"application/json\",\"frameCount\":24,\"fps\":30}", VoxtaJson.CreateOptions()) as ServerAnimationPlayMessage;
            var error = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"chatSessionError\",\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"message\":\"The action failed.\",\"details\":\"Not available\",\"retry\":false}", VoxtaJson.CreateOptions()) as ServerChatSessionErrorMessage;

            Assert.That(context.Flags[0].Name, Is.EqualTo("ready"));
            Assert.That(context.Variables["score"].GetInt32(), Is.EqualTo(7));
            Assert.That(context.Contexts[0].ContextKey, Is.EqualTo("Unity"));
            Assert.That(context.Actions[0].Name, Is.EqualTo("lower_bridge"));
            Assert.That(animation.AnimationId, Is.EqualTo(new Guid("00000000-0000-0000-0000-000000000002")));
            Assert.That(animation.ContentType, Is.EqualTo("application/json"));
            Assert.That(error.Retry, Is.False);
            Assert.That(error.Details, Is.EqualTo("Not available"));
        }
    }
}

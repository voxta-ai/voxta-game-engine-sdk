using System;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using NUnit.Framework;
using Voxta.Model.Serialization;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.Unity.Tests
{
    public sealed class ModelProtocolTests
    {
        [Test]
        public void AuthenticateMessageWritesPinnedDiscriminator()
        {
            var json = JsonSerializer.Serialize<ClientMessage>(new ClientAuthenticateMessage
            {
                Client = "Voxta Unity SDK",
                ClientVersion = "0.1.0-pre.2"
            }, VoxtaJsonSerializer.CreateSerializeOptions());

            StringAssert.Contains("\"$type\":\"authenticate\"", json);
            StringAssert.Contains("\"client\":\"Voxta Unity SDK\"", json);
        }

        [Test]
        public void SdkEmittedFramesUsePinnedDiscriminatorsAndStringEnums()
        {
            var sessionId = Guid.NewGuid();
            var messageId = Guid.NewGuid();

            AssertWireFrame(new ClientAuthenticateMessage
            {
                Client = "Voxta Unity SDK",
                ClientVersion = "0.1.0-pre.2"
            }, "authenticate", payload =>
            {
                Assert.That(payload.GetProperty("client").GetString(), Is.EqualTo("Voxta Unity SDK"));
            });
            AssertWireFrame(new ClientStartChatMessage
            {
                CharacterId = Guid.NewGuid(),
                ScenarioId = Guid.NewGuid()
            }, "startChat");
            AssertWireFrame(new ClientSendMessage
            {
                SessionId = sessionId,
                Text = "Hello",
                Role = ChatMessageRole.User
            }, "sendMessage", payload =>
            {
                Assert.That(payload.GetProperty("role").GetString(), Is.EqualTo("User"));
            });
            AssertWireFrame(new ClientUpdateContextMessage
            {
                SessionId = sessionId,
                ContextKey = "Unity",
                Actions = new[]
                {
                    new ScenarioActionDefinition
                    {
                        Name = "wave",
                        Arguments = new[]
                        {
                            new FunctionArgumentDefinition
                            {
                                Name = "hand",
                                Type = FunctionArgumentType.String,
                                Required = true
                            }
                        }
                    }
                },
                Contexts = new[]
                {
                    new ContextDefinition
                    {
                        Name = "world",
                        Text = "The bridge is raised.",
                        ApplyTo = PromptCategories.Replies,
                        Position = PromptPositions.System
                    }
                }
            }, "updateContext", payload =>
            {
                var argument = payload.GetProperty("actions")[0].GetProperty("arguments")[0];
                Assert.That(argument.GetProperty("type").GetString(), Is.EqualTo("String"));
                Assert.That(payload.GetProperty("contexts")[0].GetProperty("applyTo").GetString(), Is.EqualTo("Replies"));
                Assert.That(payload.GetProperty("contexts")[0].GetProperty("position").GetString(), Is.EqualTo("System"));
            });
            AssertWireFrame(new ClientTriggerActionMessage
            {
                SessionId = sessionId,
                MessageId = messageId,
                Value = "wave",
                Arguments = new[] { new ActionInvocationArgument { Name = "hand", Value = "left" } }
            }, "triggerAction");
            AssertWireFrame(new ClientAppTriggerCompleteMessage
            {
                SessionId = sessionId,
                TriggerId = Guid.NewGuid()
            }, "appTriggerComplete");
            AssertWireFrame(new ClientSpeechPlaybackStartMessage
            {
                SessionId = sessionId,
                MessageId = messageId,
                StartIndex = 0,
                EndIndex = 5,
                Duration = 1.25d,
                IsNarration = true
            }, "speechPlaybackStart");
            AssertWireFrame(new ClientSpeechPlaybackCompleteMessage
            {
                SessionId = sessionId,
                MessageId = messageId
            }, "speechPlaybackComplete");
            AssertWireFrame(new ClientInterruptMessage { SessionId = sessionId }, "interrupt");
        }

        [Test]
        public void UnityUrlPlaybackAdvertisesOnlyWav()
        {
            var capabilities = new ClientCapabilities { AcceptedAudioContentTypes = new[] { "audio/x-wav" } };

            CollectionAssert.AreEqual(new[] { "audio/x-wav" }, capabilities.AcceptedAudioContentTypes);
        }

        [Test]
        public void ConcreteAuthenticateMessageDoesNotWriteAPolymorphicDiscriminator()
        {
            var json = JsonSerializer.Serialize(new ClientAuthenticateMessage
            {
                Client = "Voxta Unity SDK",
                ClientVersion = "0.1.0-pre.2"
            }, VoxtaJsonSerializer.CreateSerializeOptions());

            StringAssert.DoesNotContain("\"$type\"", json);
        }

        [Test]
        public void SendMessageWritesThePinnedUserRoleValue()
        {
            var json = JsonSerializer.Serialize(new ClientSendMessage
            {
                SessionId = Guid.NewGuid(),
                Text = "Hello",
                Role = ChatMessageRole.User
            }, VoxtaJsonSerializer.CreateSerializeOptions());

            StringAssert.Contains("\"role\":\"User\"", json);
        }

        [Test]
        public void SpeechMessagesWritePinnedDiscriminatorsAndFields()
        {
            var start = JsonSerializer.Serialize<ClientMessage>(new ClientSpeechPlaybackStartMessage
            {
                SessionId = Guid.NewGuid(), MessageId = Guid.NewGuid(), StartIndex = 2, EndIndex = 7,
                Duration = 1.25d, IsNarration = true
            }, VoxtaJsonSerializer.CreateSerializeOptions());
            var interrupt = JsonSerializer.Serialize<ClientMessage>(new ClientInterruptMessage { SessionId = Guid.NewGuid() }, VoxtaJsonSerializer.CreateSerializeOptions());

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
            }, VoxtaJsonSerializer.CreateSerializeOptions());

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
            }, VoxtaJsonSerializer.CreateSerializeOptions());
            var context = JsonSerializer.Serialize<ClientMessage>(new ClientUpdateContextMessage
            {
                SessionId = Guid.NewGuid(),
                ContextKey = "Unity",
                Contexts = new[] { new ContextDefinition { Name = "world", Text = "The bridge is raised.", ApplyTo = PromptCategories.Replies, Position = PromptPositions.System } }
            }, VoxtaJsonSerializer.CreateSerializeOptions());

            StringAssert.Contains("\"$type\":\"triggerAction\"", trigger);
            StringAssert.Contains("\"messageId\":", trigger);
            StringAssert.Contains("\"value\":\"wave\"", trigger);
            StringAssert.Contains("\"$type\":\"updateContext\"", context);
            StringAssert.Contains("\"contexts\":[", context);
            StringAssert.Contains("\"applyTo\":\"Replies\"", context);
            StringAssert.Contains("\"position\":\"System\"", context);
        }

        [Test]
        public void AppTriggerCompletionWritesPinnedContract()
        {
            var triggerId = Guid.NewGuid();
            var completion = JsonSerializer.Serialize<ClientMessage>(new ClientAppTriggerCompleteMessage
            {
                SessionId = Guid.NewGuid(),
                TriggerId = triggerId
            }, VoxtaJsonSerializer.CreateSerializeOptions());

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
        public void ServerMessageRegistersPinnedDiscriminator(string discriminator, Type expectedType)
        {
            var derivedTypes = typeof(ServerMessage).GetCustomAttributes(typeof(JsonDerivedTypeAttribute), false)
                .Cast<JsonDerivedTypeAttribute>();

            Assert.That(derivedTypes.Any(attribute => attribute.DerivedType == expectedType && Equals(attribute.TypeDiscriminator, discriminator)), Is.True);
        }

        [Test]
        public void AudioFrameAndRecognitionFieldsReadFromPinnedContracts()
        {
            var audio = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"audioFrame\",\"rms\":0.25,\"voiceActivity\":true,\"listening\":true,\"noiseFloorRms\":0.1,\"thresholdRms\":0.2,\"runHeldOpen\":false}", VoxtaJsonSerializer.CreateSerializeOptions()) as ServerAudioFrameMessage;
            var end = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"speechRecognitionEnd\",\"text\":\"hello\",\"reason\":\"EndOfSpeech\",\"words\":[{\"text\":\"hello\",\"confidence\":0.9}]}", VoxtaJsonSerializer.CreateSerializeOptions()) as ServerSpeechRecognitionEndMessage;

            Assert.That(audio.VoiceActivity, Is.True);
            Assert.That(audio.ThresholdRms, Is.EqualTo(0.2f));
            Assert.That(end.Text, Is.EqualTo("hello"));
            Assert.That(end.Words[0].Confidence, Is.EqualTo(0.9d));
        }

        [Test]
        public void ActionReadsPinnedFieldsAndStringArguments()
        {
            var message = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"action\",\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"contextKey\":\"Unity\",\"layer\":\"game\",\"value\":\"wave\",\"role\":\"Assistant\",\"senderId\":\"00000000-0000-0000-0000-000000000002\",\"scenarioRole\":\"Guide\",\"arguments\":[{\"name\":\"hand\",\"value\":\"left\"}]} ", VoxtaJsonSerializer.CreateSerializeOptions()) as ServerActionMessage;

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
            var message = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"appTrigger\",\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"messageId\":\"00000000-0000-0000-0000-000000000002\",\"triggerId\":\"00000000-0000-0000-0000-000000000003\",\"name\":\"setMood\",\"arguments\":[\"happy\",3,true],\"senderId\":\"00000000-0000-0000-0000-000000000004\",\"scenarioRole\":\"Guide\"}", VoxtaJsonSerializer.CreateSerializeOptions()) as ServerActionAppTriggerMessage;

            Assert.That(message.MessageId, Is.EqualTo(new Guid("00000000-0000-0000-0000-000000000002")));
            Assert.That(message.TriggerId, Is.EqualTo(new Guid("00000000-0000-0000-0000-000000000003")));
            Assert.That(message.Name, Is.EqualTo("setMood"));
            Assert.That(((JsonElement)message.Arguments[0]).GetString(), Is.EqualTo("happy"));
            Assert.That(((JsonElement)message.Arguments[1]).GetInt32(), Is.EqualTo(3));
            Assert.That(((JsonElement)message.Arguments[2]).GetBoolean(), Is.True);
        }

        [Test]
        public void CompanionMessagesReadPinnedFields()
        {
            var context = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"contextUpdated\",\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"flags\":[{\"name\":\"ready\",\"messageChatTime\":12}],\"variables\":{\"score\":7},\"contexts\":[{\"contextKey\":\"Unity\",\"text\":\"A bridge is raised.\"}],\"actions\":[{\"contextKey\":\"Unity\",\"name\":\"lower_bridge\"}],\"characters\":[],\"roles\":{\"Guide\":{\"characterId\":null,\"enabled\":true}}}", VoxtaJsonSerializer.CreateSerializeOptions()) as ServerContextUpdatedMessage;
            var animation = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"animationPlay\",\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"animationId\":\"00000000-0000-0000-0000-000000000002\",\"url\":\"/api/animations/2\",\"contentType\":\"application/json\",\"frameCount\":24,\"fps\":30}", VoxtaJsonSerializer.CreateSerializeOptions()) as ServerAnimationPlayMessage;
            var error = JsonSerializer.Deserialize<ServerMessage>("{\"$type\":\"chatSessionError\",\"sessionId\":\"00000000-0000-0000-0000-000000000001\",\"message\":\"The action failed.\",\"details\":\"Not available\",\"retry\":false}", VoxtaJsonSerializer.CreateSerializeOptions()) as ServerChatSessionErrorMessage;

            Assert.That(context.Flags[0].Name, Is.EqualTo("ready"));
            Assert.That(((JsonElement)context.Variables["score"]).GetInt32(), Is.EqualTo(7));
            Assert.That(context.Contexts[0].ContextKey, Is.EqualTo("Unity"));
            Assert.That(context.Actions[0].Name, Is.EqualTo("lower_bridge"));
            Assert.That(animation.AnimationId, Is.EqualTo(new Guid("00000000-0000-0000-0000-000000000002")));
            Assert.That(animation.ContentType, Is.EqualTo("application/json"));
            Assert.That(error.Retry, Is.False);
            Assert.That(error.Details, Is.EqualTo("Not available"));
        }

        private static void AssertWireFrame(ClientMessage message, string expectedType, Action<JsonElement> assertPayload = null)
        {
            var json = JsonSerializer.Serialize(message, VoxtaJsonSerializer.CreateSerializeOptions());
            using var document = JsonDocument.Parse(json);
            var payload = document.RootElement;

            Assert.That(payload.GetProperty("$type").GetString(), Is.EqualTo(expectedType));
            assertPayload?.Invoke(payload);
        }
    }
}

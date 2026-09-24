using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.Unity.Samples.BasicIntegration.Tests
{
    public sealed class BasicIntegrationSampleTests
    {
        [UnityTest]
        public IEnumerator WaveActionDefinitionAndFeedbackWorkWithoutALiveServer()
        {
            var definition = BasicIntegrationSample.CreateWaveActionDefinition();
            var serializedDefinition = JsonUtility.ToJson(definition);
            Assert.That(serializedDefinition, Does.Contain("\"name\":\"wave\""));
            Assert.That(serializedDefinition, Does.Contain("\"style\""));

            var gameObject = new GameObject("BasicIntegrationSampleTests");
            gameObject.SetActive(false);
            try
            {
                var sample = gameObject.AddComponent<BasicIntegrationSample>();
                typeof(BasicIntegrationSample)
                    .GetMethod("HandleWaveAction", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(sample, new object[]
                    {
                        new ServerActionMessage
                        {
                            Value = BasicIntegrationSample.WaveActionName,
                            Arguments = new[] { new ActionInvocationArgument { Name = "style", Value = "enthusiastic" } }
                        }
                    });

                var feedback = (string)typeof(BasicIntegrationSample)
                    .GetField("actionFeedback", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(sample);
                Assert.That(feedback, Is.EqualTo("Invoked with style=enthusiastic."));
            }
            finally
            {
                Object.Destroy(gameObject);
            }

            yield return null;
        }
    }
}

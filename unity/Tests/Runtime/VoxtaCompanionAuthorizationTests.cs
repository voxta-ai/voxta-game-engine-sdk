using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Voxta.Unity.Tests
{
    public sealed class VoxtaCompanionAuthorizationTests
    {
        [UnityTest]
        public IEnumerator AuthorizedTokenCanBeAppliedBeforeTheCompanionIsEnabled()
        {
            var gameObject = new GameObject("VoxtaCompanionAuthorizationTests");
            gameObject.SetActive(false);
            try
            {
                var companion = gameObject.AddComponent<VoxtaCompanion>();

                companion.SetApiKey("approved-device-token");

                var apiKey = (string)typeof(VoxtaCompanion)
                    .GetField("apiKey", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(companion);
                Assert.That(apiKey, Is.EqualTo("approved-device-token"));
                yield return null;
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }

        [Test]
        public void RejectsAnEmptyAuthorizedToken()
        {
            var gameObject = new GameObject("VoxtaCompanionAuthorizationTests");
            gameObject.SetActive(false);
            try
            {
                var companion = gameObject.AddComponent<VoxtaCompanion>();

                Assert.That(() => companion.SetApiKey(" "), Throws.ArgumentException);
            }
            finally
            {
                Object.DestroyImmediate(gameObject);
            }
        }
    }
}

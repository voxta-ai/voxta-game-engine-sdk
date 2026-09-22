using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Voxta.Unity.Tests
{
    public sealed class VoxtaCompanionPrefabTests
    {
        [UnityTest]
        public IEnumerator PrefabContainsBoundComponentsWithSafeDefaults()
        {
            var prefab = Resources.Load<GameObject>("VoxtaCompanion");
            Assert.That(prefab, Is.Not.Null);

            var instance = Object.Instantiate(prefab);
            yield return null;

            var companion = instance.GetComponent<VoxtaCompanion>();
            var speechPlayer = instance.GetComponent<VoxtaSpeechPlayer>();
            var microphone = instance.GetComponent<VoxtaMicrophone>();
            var actions = instance.GetComponent<VoxtaActions>();
            var audioSource = instance.GetComponent<AudioSource>();

            Assert.That(companion, Is.Not.Null);
            Assert.That(speechPlayer, Is.Not.Null);
            Assert.That(microphone, Is.Not.Null);
            Assert.That(actions, Is.Not.Null);
            Assert.That(audioSource, Is.Not.Null);
            Assert.That(companion.SpeechPlayer, Is.SameAs(speechPlayer));
            Assert.That(companion.Microphone, Is.SameAs(microphone));
            Assert.That(companion.Actions, Is.SameAs(actions));
            Assert.That(speechPlayer.AudioSource, Is.SameAs(audioSource));
            Assert.That(actions.ContextKey, Is.EqualTo("Unity"));
            Assert.That(companion.enabled, Is.False);
            Assert.That(microphone.enabled, Is.False);
            Assert.That(audioSource.playOnAwake, Is.False);
            Assert.That(instance.GetComponent<AudioListener>(), Is.Null);

            Object.Destroy(instance);
        }
    }
}

using System;
using System.Text.Json;
using UnityEngine;
using Voxta.Model.Serialization;
using Voxta.Model.WebsocketMessages.ClientMessages;

namespace Voxta.CompatibilityProbe
{
    public sealed class RuntimeModelClosureProbe : MonoBehaviour
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void VerifySerialization()
        {
            var options = VoxtaJsonSerializer.CreateSerializeOptions();
            var source = new ClientAuthenticateMessage
            {
                Client = "runtime-model-unity-2022-probe",
                ClientVersion = Application.unityVersion,
                Scope = new[] { "chat" }
            };

            var json = JsonSerializer.Serialize(source, options);
            var roundTrip = JsonSerializer.Deserialize<ClientAuthenticateMessage>(json, options);

            if (roundTrip == null || roundTrip.Client != source.Client || roundTrip.Scope?.Length != 1)
                throw new InvalidOperationException("Voxta.Model JSON 10 round-trip failed.");

            Debug.Log($"Runtime model closure probe passed: {json}");

#if UNITY_STANDALONE
            Application.Quit();
#endif
        }
    }
}

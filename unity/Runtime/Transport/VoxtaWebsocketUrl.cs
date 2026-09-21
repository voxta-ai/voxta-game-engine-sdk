using System;

namespace Voxta.Unity.Transport
{
    /// <summary>Canonical Voxta server URL normalization for the SignalR hub.</summary>
    public static class VoxtaWebsocketUrl
    {
        public static Uri ToHubUri(Uri serverUrl)
        {
            if (serverUrl == null)
                throw new ArgumentNullException(nameof(serverUrl));

            string scheme;
            switch (serverUrl.Scheme.ToLowerInvariant())
            {
                case "http": scheme = "ws"; break;
                case "https": scheme = "wss"; break;
                case "ws": scheme = "ws"; break;
                case "wss": scheme = "wss"; break;
                default: throw new ArgumentException("Invalid scheme for a Voxta server URL.", nameof(serverUrl));
            }

            var builder = new UriBuilder(serverUrl)
            {
                Scheme = scheme,
                Path = serverUrl.AbsolutePath.TrimEnd('/') + "/hub",
                Query = string.Empty,
                Fragment = string.Empty
            };
            return builder.Uri;
        }

        public static Uri ToAudioInputStreamUri(Uri serverUrl, Guid sessionId)
        {
            if (sessionId == Guid.Empty)
                throw new ArgumentException("A chat session ID is required.", nameof(sessionId));

            var hub = ToHubUri(serverUrl);
            var builder = new UriBuilder(hub)
            {
                Path = hub.AbsolutePath.Substring(0, hub.AbsolutePath.Length - "/hub".Length) + "/ws/audio/input/stream",
                Query = "sessionId=" + Uri.EscapeDataString(sessionId.ToString())
            };
            return builder.Uri;
        }
    }
}

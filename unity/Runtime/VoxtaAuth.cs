using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace Voxta.Unity
{
    /// <summary>Persists the application API key acquired through device authorization.</summary>
    public interface IVoxtaTokenStore
    {
        string LoadToken();
        void SaveToken(string token);
        void ClearToken();
    }

    /// <summary>Stores a device-flow API key in Unity PlayerPrefs.</summary>
    public sealed class PlayerPrefsTokenStore : IVoxtaTokenStore
    {
        private const string DefaultKey = "Voxta.Unity.AccessToken";
        private readonly string key;

        public PlayerPrefsTokenStore(string key = DefaultKey)
        {
            if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("A PlayerPrefs key is required.", nameof(key));
            this.key = key;
        }

        public string LoadToken() => PlayerPrefs.GetString(key, string.Empty);

        public void SaveToken(string token)
        {
            if (string.IsNullOrWhiteSpace(token)) throw new ArgumentException("A token is required.", nameof(token));
            PlayerPrefs.SetString(key, token);
            PlayerPrefs.Save();
        }

        public void ClearToken()
        {
            PlayerPrefs.DeleteKey(key);
            PlayerPrefs.Save();
        }
    }

    /// <summary>The code a player enters at <see cref="VerificationUrl"/> to approve an application.</summary>
    public sealed class VoxtaDeviceCode
    {
        public string UserCode { get; internal set; }
        public Uri VerificationUrl { get; internal set; }
        internal string DeviceCode { get; set; }
    }

    /// <summary>Thrown when a device-flow endpoint returns an unexpected response.</summary>
    public sealed class VoxtaAuthException : Exception
    {
        public long StatusCode { get; }

        internal VoxtaAuthException(long statusCode, string message) : base(message) => StatusCode = statusCode;
    }

    /// <summary>Requests, polls, validates, and persists device-flow API keys.</summary>
    public sealed class VoxtaAuth
    {
        private const string ClientId = "voxta";
        private const string AppScope = "role:app";
        private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(1);
        private readonly Uri serverUrl;
        private readonly IVoxtaTokenStore tokenStore;
        private readonly IVoxtaAuthTransport transport;
        private readonly Func<TimeSpan, CancellationToken, Task> delay;

        public VoxtaAuth(Uri serverUrl, IVoxtaTokenStore tokenStore = null)
            : this(serverUrl, tokenStore ?? new PlayerPrefsTokenStore(), new UnityWebRequestAuthTransport(), Task.Delay) { }

        internal VoxtaAuth(Uri serverUrl, IVoxtaTokenStore tokenStore, IVoxtaAuthTransport transport, Func<TimeSpan, CancellationToken, Task> delay)
        {
            this.serverUrl = NormalizeServerUrl(serverUrl);
            this.tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.delay = delay ?? throw new ArgumentNullException(nameof(delay));
        }

        public string StoredToken => tokenStore.LoadToken();

        public async Task<bool> ValidateStoredTokenAsync(CancellationToken cancellationToken = default)
        {
            var token = tokenStore.LoadToken();
            var valid = await ValidateTokenAsync(token, cancellationToken);
            if (!valid && !string.IsNullOrWhiteSpace(token)) tokenStore.ClearToken();
            return valid;
        }

        public async Task<bool> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(token)) return false;
            var response = await transport.PostAsync(Endpoint("api/auth/test"), null, token, cancellationToken);
            return response.StatusCode >= 200 && response.StatusCode < 300;
        }

        public async Task<VoxtaDeviceCode> RequestDeviceCodeAsync(string label = null, CancellationToken cancellationToken = default)
        {
            var json = "{\"client_id\":\"" + ClientId + "\",\"scope\":\"" + AppScope + "\"" +
                       (string.IsNullOrWhiteSpace(label) ? string.Empty : ",\"label\":\"" + EscapeJson(label) + "\"") + "}";
            var response = await transport.PostAsync(Endpoint("api/device/code"), json, null, cancellationToken);
            EnsureSuccess(response, "request a device code");
            var payload = JsonUtility.FromJson<DeviceCodePayload>(response.Body);
            if (payload == null || string.IsNullOrWhiteSpace(payload.user_code) || string.IsNullOrWhiteSpace(payload.device_code) || !Uri.TryCreate(payload.verification_url, UriKind.Absolute, out var verificationUrl))
                throw new VoxtaAuthException(response.StatusCode, "The device-code response was missing a required value.");
            return new VoxtaDeviceCode { UserCode = payload.user_code, DeviceCode = payload.device_code, VerificationUrl = verificationUrl };
        }

        public async Task<string> WaitForTokenAsync(VoxtaDeviceCode code, CancellationToken cancellationToken = default)
        {
            if (code == null) throw new ArgumentNullException(nameof(code));
            if (string.IsNullOrWhiteSpace(code.DeviceCode)) throw new ArgumentException("The device code is missing.", nameof(code));
            while (true)
            {
                await delay(PollInterval, cancellationToken);
                var response = await transport.PostAsync(Endpoint("api/device/poll"), "{\"device_code\":\"" + EscapeJson(code.DeviceCode) + "\"}", null, cancellationToken);
                if (response.StatusCode == 204) continue;
                EnsureSuccess(response, "poll for a device token");
                var payload = JsonUtility.FromJson<TokenPayload>(response.Body);
                if (payload == null || string.IsNullOrWhiteSpace(payload.token))
                    throw new VoxtaAuthException(response.StatusCode, "The token response was missing its token.");
                if (!await ValidateTokenAsync(payload.token, cancellationToken))
                    throw new VoxtaAuthException(401, "The server returned a device token that failed validation.");
                tokenStore.SaveToken(payload.token);
                return payload.token;
            }
        }

        public async Task<string> AuthorizeAsync(string label = null, CancellationToken cancellationToken = default)
        {
            var code = await RequestDeviceCodeAsync(label, cancellationToken);
            return await WaitForTokenAsync(code, cancellationToken);
        }

        private Uri Endpoint(string path) => new Uri(serverUrl, path);

        private static Uri NormalizeServerUrl(Uri value)
        {
            if (value == null || !value.IsAbsoluteUri) throw new ArgumentException("An absolute Voxta server URL is required.", nameof(value));
            if (value.Scheme != Uri.UriSchemeHttp && value.Scheme != Uri.UriSchemeHttps) throw new ArgumentException("Device authorization requires an HTTP or HTTPS server URL.", nameof(value));
            var builder = new UriBuilder(value) { Path = value.AbsolutePath.TrimEnd('/') + "/", Query = string.Empty, Fragment = string.Empty };
            return builder.Uri;
        }

        private static void EnsureSuccess(VoxtaAuthHttpResponse response, string operation)
        {
            if (response.StatusCode >= 200 && response.StatusCode < 300) return;
            throw new VoxtaAuthException(response.StatusCode, "Unable to " + operation + " (HTTP " + response.StatusCode + ")" + (string.IsNullOrWhiteSpace(response.Body) ? "." : ": " + response.Body));
        }

        private static string EscapeJson(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t");

        [Serializable] private sealed class DeviceCodePayload { public string user_code; public string verification_url; public string device_code; }
        [Serializable] private sealed class TokenPayload { public string token; }
    }

    internal readonly struct VoxtaAuthHttpResponse
    {
        internal readonly long StatusCode;
        internal readonly string Body;
        internal VoxtaAuthHttpResponse(long statusCode, string body) { StatusCode = statusCode; Body = body; }
    }

    internal interface IVoxtaAuthTransport
    {
        Task<VoxtaAuthHttpResponse> PostAsync(Uri uri, string jsonBody, string bearerToken, CancellationToken cancellationToken);
    }

    internal sealed class UnityWebRequestAuthTransport : IVoxtaAuthTransport
    {
        public async Task<VoxtaAuthHttpResponse> PostAsync(Uri uri, string jsonBody, string bearerToken, CancellationToken cancellationToken)
        {
            using (var request = new UnityWebRequest(uri, UnityWebRequest.kHttpVerbPOST))
            {
                request.downloadHandler = new DownloadHandlerBuffer();
                if (jsonBody != null)
                {
                    request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(jsonBody));
                    request.SetRequestHeader("Content-Type", "application/json");
                }
                if (!string.IsNullOrWhiteSpace(bearerToken)) request.SetRequestHeader("Authorization", "Bearer " + bearerToken);
                using (cancellationToken.Register(request.Abort))
                {
                    var operation = request.SendWebRequest();
                    var completion = new TaskCompletionSource<bool>();
                    operation.completed += _ => completion.TrySetResult(true);
                    await completion.Task;
                }
                cancellationToken.ThrowIfCancellationRequested();
                return new VoxtaAuthHttpResponse(request.responseCode, request.downloadHandler.text);
            }
        }
    }
}

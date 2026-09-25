using System;
using System.Threading;
using System.Threading.Tasks;
using Voxta.Model.Shared;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;
using Voxta.Unity.Transport;

namespace Voxta.Unity
{
    public enum VoxtaConnectionState { Disconnected, Connecting, Authenticating, Connected, Reconnecting, Faulted }

    /// <summary>Owns one authenticated SignalR connection and routes all public events through Unity's player loop.</summary>
    public sealed class VoxtaClient : IAsyncDisposable
    {
        /// <summary>The server protocol revision represented by the pinned M1 model.</summary>
        public const string SupportedApiVersion = "2025-11";
        private readonly IVoxtaTransport transport;
        private VoxtaConnectionState state;

        public event Action<VoxtaConnectionState> ConnectionStateChanged;
        public event Action<ServerWelcomeMessage> WelcomeReceived;
        public event Action<ServerMessage> MessageReceived;
        public event Action<Exception> Error;

        public VoxtaConnectionState State => state;

        public VoxtaClient(Uri serverUrl, string accessToken, ClientCapabilities capabilities = null)
        {
            transport = new VoxtaSignalRTransport(serverUrl, accessToken, new ClientAuthenticateMessage
            {
                Client = "Voxta Unity SDK",
                ClientVersion = "0.1.0-beta.1",
                Capabilities = capabilities ?? new ClientCapabilities
                {
                    AcceptedAudioContentTypes = new[] { "audio/x-wav" }
                }
            });
            transport.MessageReceived += HandleMessage;
            transport.Error += HandleError;
            transport.Disconnected += () => SetState(VoxtaConnectionState.Disconnected);
            transport.Reconnecting += () => SetState(VoxtaConnectionState.Reconnecting);
            transport.Reconnected += () => SetState(VoxtaConnectionState.Authenticating);
        }

        internal VoxtaClient(IVoxtaTransport transport)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            transport.MessageReceived += HandleMessage;
            transport.Error += HandleError;
            transport.Disconnected += () => SetState(VoxtaConnectionState.Disconnected);
            transport.Reconnecting += () => SetState(VoxtaConnectionState.Reconnecting);
            transport.Reconnected += () => SetState(VoxtaConnectionState.Authenticating);
        }

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            SetState(VoxtaConnectionState.Connecting);
            try
            {
                SetState(VoxtaConnectionState.Authenticating);
                await transport.ConnectAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                HandleError(exception);
                throw;
            }
        }

        public void Send(ClientMessage message) => transport.Send(message);

        public async Task DisconnectAsync()
        {
            await transport.DisconnectAsync().ConfigureAwait(false);
            SetState(VoxtaConnectionState.Disconnected);
        }

        public async ValueTask DisposeAsync()
        {
            await transport.DisposeAsync().ConfigureAwait(false);
            SetState(VoxtaConnectionState.Disconnected);
        }

        private void HandleMessage(ServerMessage message)
        {
            if (message is ServerWelcomeMessage welcome)
            {
                try
                {
                    ValidateApiVersion(welcome.ApiVersion);
                }
                catch (Exception exception)
                {
                    HandleError(exception);
                    return;
                }
                SetState(VoxtaConnectionState.Connected);
                WelcomeReceived?.Invoke(welcome);
            }
            MessageReceived?.Invoke(message);
        }

        private void ValidateApiVersion(string apiVersion)
        {
            if (!string.Equals(apiVersion, SupportedApiVersion, StringComparison.Ordinal))
                throw new NotSupportedException("Voxta server API version '" + (apiVersion ?? "(missing)") + "' is incompatible with SDK protocol " + SupportedApiVersion + ".");
        }

        private void HandleError(Exception exception)
        {
            UnityMainThreadDispatcher.Post(() =>
            {
                SetState(VoxtaConnectionState.Faulted);
                Error?.Invoke(exception);
            });
        }

        private void SetState(VoxtaConnectionState value)
        {
            UnityMainThreadDispatcher.Post(() =>
            {
                if (state == value) return;
                state = value;
                ConnectionStateChanged?.Invoke(value);
            });
        }
    }
}

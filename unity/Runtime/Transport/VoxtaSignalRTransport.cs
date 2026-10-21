using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Voxta.Model.Serialization;
using Voxta.Model.WebsocketMessages.ClientMessages;
using Voxta.Model.WebsocketMessages.ServerMessages;

namespace Voxta.GameEngine.Transport
{
    internal interface IVoxtaTransport : IAsyncDisposable
    {
        event Action<ServerMessage> MessageReceived;
        event Action<Exception> Error;
        event Action Disconnected;
        event Action Reconnecting;
        event Action Reconnected;

        Task ConnectAsync(CancellationToken cancellationToken);
        Task DisconnectAsync();
        void Send(ClientMessage message);
    }

    internal sealed class VoxtaSignalRTransport : IVoxtaTransport
    {
        private readonly HubConnection connection;
        private readonly ClientAuthenticateMessage authenticateMessage;
        private readonly ConcurrentQueue<ClientMessage> sendQueue = new ConcurrentQueue<ClientMessage>();
        private readonly SemaphoreSlim sendLock = new SemaphoreSlim(1, 1);
        private readonly CancellationTokenSource cancellation = new CancellationTokenSource();
        private int isSending;

        public event Action<ServerMessage> MessageReceived;
        public event Action<Exception> Error;
        public event Action Disconnected;
        public event Action Reconnecting;
        public event Action Reconnected;

        internal VoxtaSignalRTransport(Uri serverUrl, string accessToken, ClientAuthenticateMessage authenticateMessage)
        {
            if (serverUrl == null) throw new ArgumentNullException(nameof(serverUrl));
            if (authenticateMessage == null) throw new ArgumentNullException(nameof(authenticateMessage));

            this.authenticateMessage = authenticateMessage;
            connection = new HubConnectionBuilder()
                .WithUrl(VoxtaWebsocketUrl.ToHubUri(serverUrl), options =>
                {
                    options.AccessTokenProvider = () => Task.FromResult(accessToken ?? string.Empty);
                    options.Transports = HttpTransportType.WebSockets;
                    options.SkipNegotiation = true;
                })
                .WithAutomaticReconnect(new VoxtaRetryPolicy())
                .AddJsonProtocol(options => options.PayloadSerializerOptions = VoxtaJsonSerializer.CreateSerializeOptions())
                .Build();

            connection.On<JsonElement>("ReceiveMessage", HandleReceiveMessage);
            connection.Closed += OnClosed;
            connection.Reconnecting += OnReconnecting;
            connection.Reconnected += OnReconnected;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellation.Token, cancellationToken))
            {
                while (!linked.IsCancellationRequested)
                {
                    try
                    {
                        await connection.StartAsync(linked.Token).ConfigureAwait(false);
                        break;
                    }
                    catch (OperationCanceledException) when (linked.IsCancellationRequested)
                    {
                        return;
                    }
                    catch (Exception exception)
                    {
                        UnityMainThreadDispatcher.Post(() => Error?.Invoke(exception));
                        await Task.Delay(TimeSpan.FromSeconds(2), linked.Token).ConfigureAwait(false);
                    }
                }

                if (!linked.IsCancellationRequested)
                    await SendMessageAsync(authenticateMessage, linked.Token).ConfigureAwait(false);
            }
        }

        public void Send(ClientMessage message)
        {
            if (message == null) throw new ArgumentNullException(nameof(message));
            sendQueue.Enqueue(message);
            if (Interlocked.Exchange(ref isSending, 1) == 0)
                _ = ProcessSendQueueAsync();
        }

        private void HandleReceiveMessage(JsonElement payload)
        {
            try
            {
                var message = JsonSerializer.Deserialize<ServerMessage>(payload.GetRawText(), VoxtaJsonSerializer.CreateSerializeOptions());
                if (message == null) return;
                UnityMainThreadDispatcher.Post(() => MessageReceived?.Invoke(message));
            }
            catch (JsonException exception)
            {
                // This SDK intentionally models only a pinned protocol subset. Ignore
                // server frames outside it so they cannot block later supported frames.
                if (payload.TryGetProperty("$type", out var type) && type.GetString() == "action")
                    UnityMainThreadDispatcher.Post(() => Error?.Invoke(exception));
            }
            catch (Exception exception)
            {
                UnityMainThreadDispatcher.Post(() => Error?.Invoke(exception));
            }
        }

        private async Task ProcessSendQueueAsync()
        {
            try
            {
                while (sendQueue.TryDequeue(out var message))
                {
                    await sendLock.WaitAsync(cancellation.Token).ConfigureAwait(false);
                    try
                    {
                        if (connection.State != HubConnectionState.Connected)
                            throw new VoxtaTransportDisconnectedException();
                        await SendMessageAsync(message, cancellation.Token).ConfigureAwait(false);
                    }
                    finally
                    {
                        sendLock.Release();
                    }
                }
            }
            catch (Exception exception)
            {
                UnityMainThreadDispatcher.Post(() => Error?.Invoke(exception));
            }
            finally
            {
                Interlocked.Exchange(ref isSending, 0);
                if (!sendQueue.IsEmpty && Interlocked.Exchange(ref isSending, 1) == 0)
                    _ = ProcessSendQueueAsync();
            }
        }

        // SignalR 8 serializes hub arguments as their runtime type, which drops the "$type"
        // discriminator the server dispatches on. Serializing through the base type keeps it.
        private Task SendMessageAsync(ClientMessage message, CancellationToken cancellationToken) =>
            connection.SendAsync("SendMessage", ToHubArgument(message), cancellationToken);

        internal static JsonElement ToHubArgument(ClientMessage message) =>
            JsonSerializer.SerializeToElement(message, VoxtaJsonSerializer.SerializeOptions);

        private Task OnClosed(Exception exception)
        {
            if (exception != null && !cancellation.IsCancellationRequested)
                UnityMainThreadDispatcher.Post(() => Disconnected?.Invoke());
            return Task.CompletedTask;
        }

        private Task OnReconnecting(Exception exception)
        {
            UnityMainThreadDispatcher.Post(() => Reconnecting?.Invoke());
            return Task.CompletedTask;
        }

        private async Task OnReconnected(string connectionId)
        {
            try
            {
                await SendMessageAsync(authenticateMessage, cancellation.Token).ConfigureAwait(false);
                UnityMainThreadDispatcher.Post(() => Reconnected?.Invoke());
            }
            catch (Exception exception)
            {
                UnityMainThreadDispatcher.Post(() => Error?.Invoke(exception));
            }
        }

        public async Task DisconnectAsync()
        {
            cancellation.Cancel();
            await connection.StopAsync().ConfigureAwait(false);
        }

        public async ValueTask DisposeAsync()
        {
            await DisconnectAsync().ConfigureAwait(false);
            await connection.DisposeAsync().ConfigureAwait(false);
            cancellation.Dispose();
            sendLock.Dispose();
        }
    }

    internal sealed class VoxtaRetryPolicy : IRetryPolicy
    {
        public TimeSpan? NextRetryDelay(RetryContext retryContext) => TimeSpan.FromSeconds(1);
    }

    internal sealed class VoxtaTransportDisconnectedException : InvalidOperationException
    {
        internal VoxtaTransportDisconnectedException() : base("Voxta is not connected to the WebSocket.") { }
    }
}

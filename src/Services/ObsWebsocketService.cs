using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Net.WebSockets;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization.Metadata;
using Watch3.Models.Obs;

namespace Watch3.Services
{
    public sealed class ObsWebsocketService
    {
        [MemberNotNullWhen(true, nameof(_session))]
        public bool IsConnected => !(_session is not { } session || session.IsDisposed);

        private readonly IServiceProvider _services;
        private readonly HelperService _obsHelper;
        private readonly IHostApplicationLifetime _lifetime;

        private ObsWebsocketSession? _session;

        public ObsWebsocketService(IServiceProvider services, HelperService helper, IHostApplicationLifetime lifetime)
        {
            _services = services;
            _obsHelper = helper;
            _lifetime = lifetime;
        }

        public async ValueTask<ObsWebsocketSession> GetSession()
        {
            await _obsHelper.ObsLock.WaitAsync();

            try
            {
                if (!IsConnected)
                {
                    _session = ActivatorUtilities.CreateInstance<ObsWebsocketSession>(_services);
                    await _session.Connect(_lifetime.ApplicationStopping);
                }
            }
            finally
            {
                _obsHelper.ObsLock.Release();
            }

            return _session;
        }
    }

    public sealed class ObsWebsocketSession : IAsyncDisposable
    {
        public bool IsDisposed;

        private ClientWebSocket? _client;

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly HelperService _helper;

        private event EventHandler<ObsWsRoot>? OnMessage;

        public ObsWebsocketSession(HelperService helper)
        {
            _helper = helper;
        }

        public async Task Connect(CancellationToken token)
        {
            ObsWsException.ThrowIfInit(_client);
            _client = new ClientWebSocket();

            token.Register(async () =>
            {
                await DisposeAsync();
            });

            try
            {
                await _client.ConnectAsync(_helper.ObsUri, CancellationToken.None);
                _ = Task.Run(async () =>
                {
                    while (!token.IsCancellationRequested)
                    {
                        var buffer = new Memory<byte>(new byte[2048]);
                        var writer = new ArrayBufferWriter<byte>();

                        ValueWebSocketReceiveResult result;
                        do
                        {
                            result = await _client.ReceiveAsync(buffer, _cts.Token);
                            writer.Write(buffer[..result.Count].Span);
                        } while (!result.EndOfMessage);

                        var reader = new Utf8JsonReader(writer.WrittenSpan);

                        var op = 0;

                        while (reader.Read())
                        {
                            if (reader.TokenType == JsonTokenType.PropertyName)
                            {
                                var prop = reader.GetString();

                                if (string.Equals(prop, "d", StringComparison.Ordinal))
                                {
                                    reader.Skip();
                                }

                                if (string.Equals(prop, "op", StringComparison.Ordinal))
                                {
                                    reader.Read();
                                    op = reader.GetInt32();
                                    break;
                                }
                            }
                        }

                        if (OnMessage is { } onMessage  && onMessage.GetInvocationList().Length > 0)
                        {
                            OnMessage?.Invoke(null, JsonSerializer.Deserialize(writer.WrittenMemory.Span, Json.JsonAppContext.ObsWsRoot)!);
                        }
                    }
                }, token);
            }
            catch
            {
            }

            await SendIdentifyRequest(token);
        }

        public async Task<T> SendRequest<T>(string name, JsonTypeInfo<T> typeInfo, CancellationToken token)
        {
            return await SendRequest(name, typeInfo, null, token);
        }

        public async Task<T> SendRequest<T>(string name, JsonTypeInfo<T> typeInfo, JsonObject? RequestData, CancellationToken token = default)
        {
            var id = Guid.NewGuid();
            var request = new ObsWsRoot
            (
                Op: 6,
                D: JsonSerializer.SerializeToNode(new ObsWsRequest(name, id, RequestData ?? []), Json.JsonAppContext.ObsWsRequest)!
            );
            return await WaitSendRequest(request, id, typeInfo, token);
        }

        public async Task<ObsWsRequestResponse> StartStream(CancellationToken token = default) =>
            await SendRequest("StartStream", Json.JsonAppContext.ObsWsRequestResponse, token);

        public async Task<ObsWsRequestResponse> StopStream(CancellationToken token = default) =>
            await SendRequest("StopStream", Json.JsonAppContext.ObsWsRequestResponse, token);

        public async Task<ObsWsGetStreamStatusResponse> GetStreamStatus(CancellationToken token = default) =>
            (await SendRequest("GetStreamStatus", Json.JsonAppContext.ObsWsRequestResponseObsWsGetStreamStatusResponse, token)).ResponseData;

        public async Task<ObsWsRequestResponse> StartRecord(CancellationToken token = default) =>
            await SendRequest("StartRecord", Json.JsonAppContext.ObsWsRequestResponse, token);

        public async Task<ObsWsRequestResponse> StopRecord(CancellationToken token = default) =>
            await SendRequest("StopRecord", Json.JsonAppContext.ObsWsRequestResponse, token);

        public async Task<ObsWsGetRecordStatusResponse> GetRecordStatus(CancellationToken token = default) =>
            (await SendRequest("GetRecordStatus", Json.JsonAppContext.ObsWsRequestResponseObsWsGetRecordStatusResponse, token)).ResponseData;

        public async Task<ObsWsRequestResponse> SplitRecordFile(CancellationToken token = default) => 
            await SendRequest("SplitRecordFile", Json.JsonAppContext.ObsWsRequestResponse, token);

        public async ValueTask DisposeAsync()
        {
            try { await StopStream(); } catch { }
            await Disconnect();
            _client?.Dispose();
            _cts.Cancel();
            _cts.Dispose();
            IsDisposed = true;
        }

        private async Task<T> WaitSendRequest<T>(ObsWsRoot request,
                                                 Guid id,
                                                 JsonTypeInfo<T> typeInfo,
                                                 CancellationToken token = default)
        {
            ArgumentNullException.ThrowIfNull(_client);
            ObjectDisposedException.ThrowIf(IsDisposed, _client);

            var tcs = new TaskCompletionSource<T>();

            void onMessage(object? s, ObsWsRoot e)
            {
                Console.WriteLine(e.ToString());

                if (e.D["requestId"]?.GetValue<Guid>() is not { } requestId || requestId == id)
                {
                    tcs.SetResult(e.D.Deserialize(typeInfo)!);
                }
            }
            OnMessage += onMessage;

            await _client.SendAsync(JsonSerializer.SerializeToUtf8Bytes(request, Json.JsonAppContext.ObsWsRoot),
                                    WebSocketMessageType.Text,
                                    endOfMessage: true,
                                    token);

            var result = await tcs.Task;

            OnMessage -= onMessage;

            return result;
        }

        private async Task Disconnect()
        {
            ArgumentNullException.ThrowIfNull(_client);
            await _client.CloseAsync(WebSocketCloseStatus.NormalClosure, statusDescription: null, CancellationToken.None);
        }

        private async Task<ObsWsIdentified> SendIdentifyRequest(CancellationToken token = default)
        {
            var request = new ObsWsRoot
            (
                Op: 1,
                D: JsonSerializer.SerializeToNode(new ObsWsIdentify(1, null, int.MaxValue), Json.JsonAppContext.ObsWsIdentify)!
            );
            return await WaitSendRequest(request, Guid.NewGuid(), Json.JsonAppContext.ObsWsIdentified, token);
        }

        private sealed record ObsWsMessage(int Op, ObsWsRoot Message);

        private sealed class ObsWsException() : Exception
        {
            public static void ThrowIfInit(ClientWebSocket? client)
            {
                if (client is not null)
                    throw new ObsWsException();
            }
        }
    }
}

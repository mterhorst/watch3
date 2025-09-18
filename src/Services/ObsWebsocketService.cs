using System.Buffers;
using System.Collections.Concurrent;
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
        private readonly ILogger<ObsWebsocketSession> _logger;

        private readonly ConcurrentDictionary<Guid, TaskCompletionSource<ObsWsRoot>> _pendingRequests = new();

        public ObsWebsocketSession(HelperService helper, ILogger<ObsWebsocketSession> logger)
        {
            _helper = helper;
            _logger = logger;
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

                        var response = JsonSerializer.Deserialize(writer.WrittenMemory.Span, Json.Default.ObsWsRoot)!;


                        if (response.Op == 2)
                        {
                            if (_pendingRequests.TryGetValue(Guid.Empty, out var tcs))
                            {
                                tcs.TrySetResult(response);
                            }
                        }
                        else if (response.D["requestId"]?.GetValue<Guid>() is { } requestId)
                        {
                            if (_pendingRequests.TryGetValue(requestId, out var tcs))
                            {
                                tcs.TrySetResult(response);
                            }
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
                D: JsonSerializer.SerializeToNode(new ObsWsRequest(name, id, RequestData ?? []), Json.Default.ObsWsRequest)!
            );
            return await WaitSendRequest(request, id, typeInfo, token);
        }

        public async Task<ObsWsRequestResponse> StartStream(CancellationToken token = default) =>
            await SendRequest("StartStream", Json.Default.ObsWsRequestResponse, token);

        public async Task<ObsWsRequestResponse> StopStream(CancellationToken token = default) =>
            await SendRequest("StopStream", Json.Default.ObsWsRequestResponse, token);

        public async Task<ObsWsGetStreamStatusResponse> GetStreamStatus(CancellationToken token = default) =>
            (await SendRequest("GetStreamStatus", Json.Default.ObsWsRequestResponseObsWsGetStreamStatusResponse, token)).ResponseData;

        public async Task<ObsWsRequestResponse> StartRecord(CancellationToken token = default) =>
            await SendRequest("StartRecord", Json.Default.ObsWsRequestResponse, token);

        public async Task<ObsWsRequestResponse> StopRecord(CancellationToken token = default) =>
            await SendRequest("StopRecord", Json.Default.ObsWsRequestResponse, token);

        public async Task<ObsWsGetRecordStatusResponse> GetRecordStatus(CancellationToken token = default) =>
            (await SendRequest("GetRecordStatus", Json.Default.ObsWsRequestResponseObsWsGetRecordStatusResponse, token)).ResponseData;

        public async Task<ObsWsRequestResponse> SplitRecordFile(CancellationToken token = default) =>
            await SendRequest("SplitRecordFile", Json.Default.ObsWsRequestResponse, token);

        public async ValueTask DisposeAsync()
        {
            _pendingRequests.Clear();
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

            var tcs = new TaskCompletionSource<ObsWsRoot>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pendingRequests.TryAdd(id, tcs);

            await _client.SendAsync(JsonSerializer.SerializeToUtf8Bytes(request, Json.Default.ObsWsRoot),
                                    WebSocketMessageType.Text,
                                    endOfMessage: true,
                                    token);

            var result = await tcs.Task.WaitAsync(token);

            _logger.LogInformation("Received OBS websocket message {id} {msg}", id, result);

            _pendingRequests.TryRemove(id, out _);

            return result.D.Deserialize(typeInfo)!;
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
                D: JsonSerializer.SerializeToNode(new ObsWsIdentify(1, null, int.MaxValue), Json.Default.ObsWsIdentify)!
            );
            return await WaitSendRequest(request, Guid.Empty, Json.Default.ObsWsIdentified, token);
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

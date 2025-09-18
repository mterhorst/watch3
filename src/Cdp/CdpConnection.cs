using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading.Channels;
using Watch3.Models.Cdp;

namespace Watch3.Cdp
{
    public sealed class CdpConnection : IDisposable
    {
        private readonly ConcurrentDictionary<Guid, Channel<IdMessageResponse>> _idMessageChannels = new();
        private readonly ConcurrentDictionary<Guid, Channel<MethodMessageResponse>> _methodMessageChannels = new();
        private readonly ClientWebSocket _client;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger<CdpConnection> _logger;

        private readonly Channel<DTCommand> _commands = Channel.CreateBounded<DTCommand>(new BoundedChannelOptions(10)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        public CdpConnection(IHostApplicationLifetime lifetime, ILogger<CdpConnection> logger, ClientWebSocket client)
        {
            _client = client;
            _lifetime = lifetime;
            _logger = logger;
        }

        public async Task<TResponse> WaitIdMessage<TResponse>(DTCommand request,
                                                              Func<IdMessageResponse, bool> check,
                                                              JsonTypeInfo<TResponse> typeInfo,
                                                              CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<IdMessageResponse>();
            var guid = Guid.NewGuid();
            _idMessageChannels.TryAdd(guid, channel);

            await SendMessage(request, token);

            var result = default(TResponse);
            await foreach (var msg in channel.Reader.ReadAllAsync(token))
            {
                if (check(msg))
                {
                    result = msg.Json.Deserialize(typeInfo);
                    break;
                }
            }

            channel.Writer.Complete();
            _idMessageChannels.TryRemove(guid, out _);

            return result!;
        }

        public async Task<TResponse> WaitMethodMessage<TResponse>(DTCommand request,
                                                                  Func<MethodMessageResponse, bool> check,
                                                                  JsonTypeInfo<TResponse> typeInfo,
                                                                  CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<MethodMessageResponse>();
            var guid = Guid.NewGuid();
            _methodMessageChannels.TryAdd(guid, channel);

            await SendMessage(request, token);

            var result = default(TResponse);
            await foreach (var msg in channel.Reader.ReadAllAsync(token))
            {
                if (check(msg))
                {
                    result = msg.Json.Deserialize(typeInfo);
                    break;
                }
            }

            channel.Writer.Complete();
            _methodMessageChannels.TryRemove(guid, out _);

            return result!;
        }

        public async IAsyncEnumerable<TResponse> WaitMethodMessages<TResponse>(Func<MethodMessageResponse, bool> check,
                                                                               JsonTypeInfo<TResponse> typeInfo,
                                                                               [EnumeratorCancellation] CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<MethodMessageResponse>();
            var guid = Guid.NewGuid();
            _methodMessageChannels.TryAdd(guid, channel);

            await foreach (var msg in channel.Reader.ReadAllAsync(token))
            {
                if (check(msg))
                {
                    yield return msg.Json.Deserialize(typeInfo)!;
                }
            }

            channel.Writer.Complete();
            _methodMessageChannels.TryRemove(guid, out _);
        }

        public async IAsyncEnumerable<TResponse> WaitMethodMessages<TResponse>(DTCommand request,
                                                                               Func<MethodMessageResponse, bool> check,
                                                                               JsonTypeInfo<TResponse> typeInfo,
                                                                               [EnumeratorCancellation] CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<MethodMessageResponse>();
            var guid = Guid.NewGuid();
            _methodMessageChannels.TryAdd(guid, channel);

            await SendMessage(request, token);

            await foreach (var msg in channel.Reader.ReadAllAsync(token))
            {
                if (check(msg))
                {
                    yield return msg.Json.Deserialize(typeInfo)!;
                }
            }

            channel.Writer.Complete();
            _methodMessageChannels.TryRemove(guid, out _);
        }

        public void Dispose()
        {
            _client.Dispose();
        }

        private async Task SendMessage(DTCommand request, CancellationToken token)
        {
            var msg = await CreateRequestMessage(request, token);
            await _client.SendAsync(msg, WebSocketMessageType.Text, endOfMessage: true, token).AsTask();
        }

        public async Task StartReciever()
        {
            while (!_lifetime.ApplicationStopping.IsCancellationRequested)
            {
                await using var stream = new MemoryStream();
                var buffer = new Memory<byte>(new byte[2048]);

                ValueWebSocketReceiveResult result;
                do
                {
                    result = await _client.ReceiveAsync(buffer, _lifetime.ApplicationStopping);
                    await stream.WriteAsync(buffer[..result.Count], _lifetime.ApplicationStopping);

                } while (!result.EndOfMessage);

                stream.Seek(0, SeekOrigin.Begin);

                if (await JsonSerializer.DeserializeAsync(stream, Json.Default.JsonObject, _lifetime.ApplicationStopping) is not { } jsonObject)
                    continue;

                var errors = new List<string>();

                if (jsonObject.TryGetPropertyValue("error", out var error) && error?["message"]?.GetValue<string>() is { } message)
                {
                    errors.Add(message);
                    while (_commands.Reader.Count > 0)
                    {
                        var command = await _commands.Reader.ReadAsync(_lifetime.ApplicationStopping);
                        if (command.Id == jsonObject["id"]?.GetValue<int>())
                        {
                            errors.Add(command.ToString());
                            break;
                        }
                    }

                    _logger.LogInformation(string.Join(" | ", errors));
                }

                if (jsonObject.TryGetPropertyValue("id", out var idProp) && idProp?.GetValue<int>() is int id)
                {
                    var sessionId = jsonObject["sessionId"]?.GetValue<string>();

                    _logger.LogInformation("RECEIVE: {id} | {sessionId}", id, sessionId);

                    foreach (var idMessageChannel in _idMessageChannels)
                    {
                        await idMessageChannel.Value.Writer.WriteAsync(new IdMessageResponse
                        (
                            Id: id,
                            SessionId: sessionId,
                            Json: jsonObject,
                            Errors: errors
                        ));
                    }
                }
                else if (jsonObject.TryGetPropertyValue("method", out var methodProp) && methodProp?.GetValue<string>() is string method)
                {
                    var sessionId = jsonObject["sessionId"]?.GetValue<string>();

                    _logger.LogInformation("RECEIVE: {method} | {sessionId}", method, sessionId);

                    foreach (var idMessageChannel in _methodMessageChannels)
                    {
                        await idMessageChannel.Value.Writer.WriteAsync(new MethodMessageResponse
                        (
                            Method: method,
                            SessionId: sessionId,
                            Json: jsonObject,
                            Errors: errors
                        ));
                    }
                }
            }
        }

        private async Task<Memory<byte>> CreateRequestMessage(DTCommand message, CancellationToken token)
        {
            _logger.LogInformation("SEND: {message.Id} | {message.Method} | {message.SessionId}", message.Id, message.Method, message.SessionId);

            await _commands.Writer.WriteAsync(message, token);

            await using var stream = new MemoryStream();
            await JsonSerializer.SerializeAsync(stream, message, Json.Default.DTCommand, token);
            return new Memory<byte>(stream.ToArray());
        }
    }
}

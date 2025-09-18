using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Watch3.Models.Cdp;

namespace Watch3.Cdp
{
    public sealed class CdpPage : IAsyncDisposable
    {
        public string Url { get; private set; } = "";

        internal bool _isDisposed;

        private readonly CdpConnection _connection;
        private readonly string _targetId;
        private readonly string _sessionId;
        private readonly string _frameId;

        private readonly CancellationTokenSource _pageCts;

        private readonly ConcurrentDictionary<Guid, Channel<string>> _urlChannels = new();

        private readonly Channel<int> _ctxChannel = Channel.CreateBounded<int>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest
        });

        private int _previousCtxId = 0;
        private int? _activeCtxId;
        private string? _lifecycleEvent = null;

        private CdpPage(CdpConnection connection, string targetId, string sessionId, string frameId, CancellationToken token)
        {
            _connection = connection;
            _targetId = targetId;
            _sessionId = sessionId;
            _frameId = frameId;

            _pageCts = CancellationTokenSource.CreateLinkedTokenSource(token);

            StartListeners();
        }

        public static async Task<CdpPage> CreatePage(CdpConnection connection, string targetId, string sessionId, CancellationToken token)
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var pageEnableCmd = new DTCommand
            (
                Id: id,
                Method: "Page.enable",
                SessionId: sessionId
            );
            var responsePageEnable = await connection.WaitIdMessage(pageEnableCmd, (msg) =>
            {
                return msg.Id == id;
            }, Json.Default.DTResponsePageEnable, token);

            id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var pageGetFrameTreeCmd = new DTCommand
            (
                Id: id,
                Method: "Page.getFrameTree",
                SessionId: sessionId
            );
            var responsePageGetFrameTree = await connection.WaitIdMessage(pageGetFrameTreeCmd, (msg) =>
            {
                return msg.Id == id;
            }, Json.Default.DTResponsePageGetFrameTree, token);

            return new CdpPage(connection,
                            targetId,
                            sessionId,
                            responsePageGetFrameTree.Result.FrameTree.Frame.Id,
                            token);
        }

        public async Task<string> GetContent()
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var runtimeCallFunctionOnCmd = new DTCommand
            (
                Id: id,
                Method: "Runtime.callFunctionOn",
                SessionId: _sessionId,
                Params: new DTCommandRuntimeCallFunctionOn
                (
                    FunctionDeclaration: """
                        () => {
                            let retVal = "";
                            if (document.doctype)
                                retVal = new XMLSerializer().serializeToString(document.doctype);
                            if (document.documentElement)
                                retVal += document.documentElement.outerHTML;
                            return retVal;
                        }
                        """,
                    ExecutionContextId: await WaitExecutionContextCreated(),
                    ReturnByValue: true,
                    AwaitPromise: true,
                    UserGesture: true
                )
            );
            var responseRuntimeCallFunctionOn = await _connection.WaitIdMessage(runtimeCallFunctionOnCmd, (msg) =>
            {
                return msg.Id == id;
            }, Json.Default.DTResponseRuntimeCallFunctionOn, _pageCts.Token);

            return responseRuntimeCallFunctionOn.Result.Result.Value;
        }

        public async Task SetCookie(string name, string value, string domain)
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var setCookieCmd = new DTCommand
            (
                Id: id,
                Method: "Network.setCookie",
                SessionId: _sessionId,
                Params: new DTCommandSetCookie
                (
                    Name: name,
                    Value: value,
                    Domain: domain,
                    Secure: false,
                    HttpOnly: false
                )
            );

            await _connection.WaitIdMessage(setCookieCmd, (msg) =>
            {
                return msg.Id == id;
            }, Json.Default.DTCommandSetCookie, _pageCts.Token);
        }

        public async Task SetContent(string html)
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var runtimeCallFunctionOnCmd = new DTCommand
            (
                Id: id,
                Method: "Runtime.callFunctionOn",
                SessionId: _sessionId,
                Params: new DTCommandRuntimeCallFunctionOn
                (
                    FunctionDeclaration: """
                        html => {
                            document.open();
                            document.write(html);
                            document.close();
                        }
                        """,
                    ExecutionContextId: await WaitExecutionContextCreated(),
                    ReturnByValue: true,
                    AwaitPromise: true,
                    UserGesture: true,
                    Arguments:
                    [
                        new(Value: html)
                    ]
                )
            );
            var responseRuntimeCallFunctionOn = await _connection.WaitIdMessage(runtimeCallFunctionOnCmd, (msg) =>
            {
                return msg.Id == id;
            }, Json.Default.DTResponseRuntimeCallFunctionOn, _pageCts.Token);
        }

        public async Task Navigate([StringSyntax(StringSyntaxAttribute.Uri)] string url)
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var setContentsCmd = new DTCommand
            (
                Id: id,
                Method: "Page.navigate",
                SessionId: _sessionId,
                Params: new DTCommandPageNavigate
                (
                    Url = url
                )
            );
            var responsePageNavigate = await _connection.WaitIdMessage(setContentsCmd, (msg) =>
            {
                return msg.Id == id;
            }, Json.Default.DTResponsePageNavigate, _pageCts.Token);
        }

        public async IAsyncEnumerable<string> WaitAllForNavigate([EnumeratorCancellation] CancellationToken token)
        {
            var channel = Channel.CreateUnbounded<string>();
            var guid = Guid.NewGuid();
            _urlChannels.TryAdd(guid, channel);

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, _pageCts.Token);

            while (!cts.IsCancellationRequested && await channel.Reader.WaitToReadAsync(cts.Token))
            {
                yield return await channel.Reader.ReadAsync(token);
            }

            channel.Writer.Complete();
            _urlChannels.TryRemove(guid, out _);
        }

        public async Task WaitForNavigate(CancellationToken token)
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(token, _pageCts.Token);

            await foreach (var url in WaitAllForNavigate(cts.Token))
            {
                cts.Cancel();
            }
        }

        public async Task Reload()
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var pageReloadCmd = new DTCommand
            (
                Id: id,
                Method: "Page.reload",
                SessionId: _sessionId
            );
            _ = await _connection.WaitIdMessage(pageReloadCmd, (msg) =>
            {
                return msg.Id == id;
            }, Json.Default.JsonObject, _pageCts.Token);
        }

        private void StartListeners()
        {
            _ = Task.Run((Func<Task?>)(async () =>
            {
                await foreach (var msg in _connection.WaitMethodMessages<JsonObject>((Func<MethodMessageResponse, bool>)((msg) =>
                {
                    return msg.Method == "Runtime.executionContextsCleared" && msg.SessionId == _sessionId;
                }), Json.Default.JsonObject, _pageCts.Token))
                {
                    _activeCtxId = null;
                }
            }), _pageCts.Token);

            _ = Task.Run((Func<Task?>)(async () =>
            {
                var id = RandomNumberGenerator.GetInt32(int.MaxValue);
                var runtimeEnableCmd = new DTCommand
                (
                    Id: id,
                    Method: "Runtime.enable",
                    SessionId: _sessionId
                );
                await foreach (var msg in _connection.WaitMethodMessages<DTResponseRuntimeExecutionContextCreated>(runtimeEnableCmd, (Func<MethodMessageResponse, bool>)((msg) =>
                {
                    return msg.Method == "Runtime.executionContextCreated" && msg.SessionId == _sessionId;
                }), Json.Default.DTResponseRuntimeExecutionContextCreated, _pageCts.Token))
                {
                    if (msg.Params.Context.AuxData.FrameId == _frameId)
                    {
                        _previousCtxId = msg.Params.Context.Id;
                        _activeCtxId = msg.Params.Context.Id;

                        await _ctxChannel.Writer.WriteAsync(msg.Params.Context.Id);
                    }
                }
            }), _pageCts.Token);

            _ = Task.Run((Func<Task?>)(async () =>
            {
                var id = RandomNumberGenerator.GetInt32(int.MaxValue);
                var pageSetLifecycleEventsEnabledCmd = new DTCommand
                (
                    Id: id,
                    Method: "Page.setLifecycleEventsEnabled",
                    SessionId: _sessionId,
                    Params: new DTCommandPageSetLifecycleEventsEnabled
                    (
                        Enabled: true
                    )
                );
                await foreach (var msgLifecycle in _connection.WaitMethodMessages<DTResponsePageLifecycleEvent>(pageSetLifecycleEventsEnabledCmd, (Func<MethodMessageResponse, bool>)((msg) =>
                {
                    return msg.Method == "Page.lifecycleEvent" && msg.SessionId == _sessionId;
                }), Json.Default.DTResponsePageLifecycleEvent, _pageCts.Token))
                {
                    _lifecycleEvent = msgLifecycle.Params.Name;
                }
            }), _pageCts.Token);

            _ = Task.Run((Func<Task?>)(async () =>
            {
                await foreach (var msg in _connection.WaitMethodMessages<DTResponsePageFrameNavigatedWithinDocument>((Func<MethodMessageResponse, bool>)((msg) =>
                {
                    return msg.Method == "Page.navigatedWithinDocument" && msg.SessionId == _sessionId;
                }), Json.Default.DTResponsePageFrameNavigatedWithinDocument, _pageCts.Token))
                {
                    Url = msg.Params.Url;

                    foreach (var channel in _urlChannels)
                    {
                        await channel.Value.Writer.WriteAsync(msg.Params.Url);
                    }
                }
            }), _pageCts.Token);

            _ = Task.Run((Func<Task?>)(async () =>
            {
                await foreach (var msgNavigated in _connection.WaitMethodMessages<DTResponsePageFrameNavigated>((Func<MethodMessageResponse, bool>)((msg) =>
                {
                    return msg.Method == "Page.frameNavigated" && msg.SessionId == _sessionId;
                }), Json.Default.DTResponsePageFrameNavigated, _pageCts.Token))
                {
                    Url = msgNavigated.Params.Frame.UrlFragment is null
                        ? msgNavigated.Params.Frame.Url
                        : $"{msgNavigated.Params.Frame.Url}{msgNavigated.Params.Frame.UrlFragment}";

                    foreach (var channel in _urlChannels)
                    {
                        await channel.Value.Writer.WriteAsync(msgNavigated.Params.Frame.Url);
                    }
                }
            }), _pageCts.Token);
        }

        private async Task<int> WaitExecutionContextCreated()
        {
            await WaitIdle();

            int? ctxId = null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(_pageCts.Token);
            cts.CancelAfter(TimeSpan.FromMilliseconds(250));

            try
            {
                ctxId = await _ctxChannel.Reader.ReadAsync(cts.Token);
                ctxId = await _ctxChannel.Reader.ReadAsync(cts.Token);
            }
            catch { }

            return ctxId ?? _activeCtxId ?? _previousCtxId;
        }

        public async ValueTask DisposeAsync()
        {
            _isDisposed = true;

            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var closeTargetCmd = new DTCommand
            (
                Id: id,
                Method: "Target.closeTarget",
                SessionId: _sessionId,
                Params: new DTCommandTargetCloseTarget
                (
                    TargetId: _frameId
                )
            );

            try
            {
                var responsePageNavigate = await _connection.WaitIdMessage(closeTargetCmd, (msg) =>
                {
                    return msg.Id == id;
                }, Json.Default.JsonObject, _pageCts.Token);
            }
            catch { }

            try { _pageCts.Cancel(); } catch { } finally { _pageCts.Dispose(); }

        }

        public async Task WaitIdle()
        {
            while (!_pageCts.IsCancellationRequested && !IsIdle())
            {
                await Task.Delay(25);
            }
        }

        private bool IsIdle()
        {
            var lifecycleEvents = new List<string> { "DOMContentLoaded", "networkIdle", "InteractiveTime" };
            return lifecycleEvents.Any(x => string.Equals(x, _lifecycleEvent, StringComparison.Ordinal));
        }
    }
}

using System.Collections.Concurrent;
using System.Security.Cryptography;
using Watch3.Models.Cdp;

namespace Watch3.Cdp
{
    public sealed class CdpBrowserSession
    {
        private readonly ConcurrentDictionary<string, CdpPage> _pages = new ConcurrentDictionary<string, CdpPage>();

        private readonly CdpConnection _connection;
        private readonly CancellationToken _token;

        private string? _preferFocusTargetId;

        public CdpBrowserSession(CdpConnection connection, CancellationToken token)
        {
            _connection = connection;
            _token = token;

            token.Register(async () =>
            {
                foreach (var page in _pages)
                    await page.Value.DisposeAsync();

                _connection.Dispose();
            });
        }

        public async Task<CdpPage> CreatePage()
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var targetEnableCmd = new DTCommand
            (
                Id: id,
                Method: "Target.createTarget",
                Params: new DTCommandTargetCreateTarget
                (
                    Url: "about:blank",
                    NewWindow: false
                )
            );
            var responseTargetCreateTarget = await _connection.WaitIdMessage(targetEnableCmd, (msg) =>
            {
                return msg.Id == id;
            }, Json.Default.DTResponseTargetCreateTarget, _token);

            var targetId = responseTargetCreateTarget!.Result.TargetId;
            var responseTargetAttachToTarget = await AttachToTarget(targetId);

            _preferFocusTargetId ??= (await GetTargetInfos()).LastOrDefault(x => x.Type == "page")?.TargetId;

            if (_preferFocusTargetId is not null)
            {
                id = RandomNumberGenerator.GetInt32(int.MaxValue);
                var targetActivateTargetCmd = new DTCommand
                (
                    Id: id,
                    Method: "Target.activateTarget",
                    Params: new DTCommandTargetActivateTarget
                    (
                        TargetId: _preferFocusTargetId
                    )
                );
                var test = await _connection.WaitIdMessage(targetActivateTargetCmd, (msg) =>
                {
                    return msg.Id == id;
                }, Json.Default.JsonObject, _token);
            }

            var page = await CdpPage.CreatePage(_connection, targetId, responseTargetAttachToTarget.Params.SessionId, _token);

            _pages.TryAdd(targetId, page);

            return page;
        }

        public async Task<IList<CdpPage>> GetPages()
        {
            foreach (var target in (await GetTargetInfos()).Where(x => x.Type == "page"))
            {
                if (!_pages.ContainsKey(target.TargetId))
                {
                    var responseTargetAttachToTarget = await AttachToTarget(target.TargetId);

                    var page = await CdpPage.CreatePage(_connection, target.TargetId, responseTargetAttachToTarget.Params.SessionId, _token);
                    _pages.TryAdd(target.TargetId, page);
                }
            }

            foreach (var page in _pages.Where(x => x.Value._isDisposed))
            {
                _pages.TryRemove(page.Key, out _);
            }

            return [.. _pages.Values];
        }

        private async Task<IEnumerable<DTResponseGetTargetsResultTargetInfos>> GetTargetInfos()
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var targetGetTargetsCmd = new DTCommand
            (
                Id: id,
                Method: "Target.getTargets"
            );
            var responseGetTargets = await _connection.WaitIdMessage(targetGetTargetsCmd, (msg) =>
            {
                return msg.Id == id;
            }, Json.Default.DTResponseGetTargets, _token);
            return responseGetTargets.Result.TargetInfos;
        }

        private async Task<DTResponseTargetAttachToTarget> AttachToTarget(string targetId)
        {
            var id = RandomNumberGenerator.GetInt32(int.MaxValue);
            var targetAttachToTargetCmd = new DTCommand
            (
                Id: id,
                Method: "Target.attachToTarget",
                Params: new DTCommandTargetAttachToTarget
                (
                    TargetId: targetId,
                    Flatten: true
                )
            );
            var responseTargetAttachToTarget = await _connection.WaitMethodMessage(targetAttachToTargetCmd, (msg) =>
            {
                return msg.Method == "Target.attachedToTarget" &&
                msg.Json["params"]?["targetInfo"]?["targetId"]?.GetValue<string>() == targetId;
            }, Json.Default.DTResponseTargetAttachToTarget, _token);
            return responseTargetAttachToTarget;
        }
    }
}

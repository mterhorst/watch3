namespace Watch3.Models.Cdp
{
    public sealed record DTResponseTargetAttachToTarget(string Method, DTResponseTargetAttachToTargetParams Params);
    public sealed record DTResponseTargetAttachToTargetParams(string SessionId, bool WaitingForDebugger, DTResponseTargetAttachToTargetParamsTargetInfo TargetInfo);
    public sealed record DTResponseTargetAttachToTargetParamsTargetInfo(string TargetId, string Type, string Title, string Url, bool Attached, bool CanAccessOpener, string BrowserContextId);
}

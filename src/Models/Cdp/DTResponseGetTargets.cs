namespace Watch3.Models.Cdp
{
    public sealed record DTResponseGetTargets(int Id, DTResponseGetTargetsResult Result);
    public sealed record DTResponseGetTargetsResult(IEnumerable<DTResponseGetTargetsResultTargetInfos> TargetInfos);
    public sealed record DTResponseGetTargetsResultTargetInfos(string TargetId, string Type, string Title, string Url, bool Attached,
        bool CanAccessOpener, string BrowserContextId, int Pid);
}

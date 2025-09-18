namespace Watch3.Models.Cdp
{
    public sealed record DTResponsePageLifecycleEvent(string Method, string SessionId, DTResponsePageLifecycleEventParams Params);
    public sealed record DTResponsePageLifecycleEventParams(string FrameId, string LoaderId, string Name, float Timestamp);
}

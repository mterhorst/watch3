namespace Watch3.Models.Cdp
{
    public sealed record DTResponsePageNavigate(int Id, string SessionId, DTResponsePageNavigateResult Result);
    public sealed record DTResponsePageNavigateResult(string FrameId, string LoaderId);
}

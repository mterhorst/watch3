namespace Watch3.Models.Cdp
{
    public sealed record DTResponsePageEnable(int Id, DTResponsePageEnableResult Result);
    public sealed record DTResponsePageEnableResult(string SessionId);
}

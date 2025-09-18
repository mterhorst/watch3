namespace Watch3.Models.Cdp
{
    public sealed record DTResponseTargetCreateTarget(int Id, DTResponseTargetCreateTargetResult Result);
    public sealed record DTResponseTargetCreateTargetResult(string TargetId);
}

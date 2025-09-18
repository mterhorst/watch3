namespace Watch3.Models.Cdp
{
    public sealed record DTResponseRuntimeCallFunctionOn(int Id, string sessionId, DTResponseRuntimeCallFunctionOnResult Result);
    public sealed record DTResponseRuntimeCallFunctionOnResult(DTResponseRuntimeCallFunctionOnResultResult Result);
    public sealed record DTResponseRuntimeCallFunctionOnResultResult(string Type, string Value);
}

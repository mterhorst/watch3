namespace Watch3.Models.Cdp
{
    public sealed record DTResponseRuntimeEvaluate(int Id, string sessionId, DTResponseRuntimeEvaluateResult Result);
    public sealed record DTResponseRuntimeEvaluateResult(DTResponseRuntimeEvaluateResultResult Result);
    public sealed record DTResponseRuntimeEvaluateResultResult(string Type, string ClassName, string Description, string ObjectId);
}

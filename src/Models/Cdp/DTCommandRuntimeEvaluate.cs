namespace Watch3.Models.Cdp
{
    public sealed record DTCommandRuntimeEvaluate(string Expression, int? contextId = null, bool? ReturnByValue = null, bool? AwaitPromise = null, bool? UserGesture = null);
}

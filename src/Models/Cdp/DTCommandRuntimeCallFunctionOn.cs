namespace Watch3.Models.Cdp
{
    public sealed record DTCommandRuntimeCallFunctionOn(string FunctionDeclaration,
        int ExecutionContextId, bool ReturnByValue, bool AwaitPromise, bool UserGesture, IEnumerable<DTCommandRuntimeCallFunctionOnArguments>? Arguments = null);

    public sealed record DTCommandRuntimeCallFunctionOnArguments(string Value);
}

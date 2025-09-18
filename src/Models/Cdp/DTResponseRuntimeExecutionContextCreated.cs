namespace Watch3.Models.Cdp
{
    public sealed record DTResponseRuntimeExecutionContextCreated(string Method, string SessionId, DTResponseRuntimeExecutionContextParams Params);
    public sealed record DTResponseRuntimeExecutionContextParams(DTResponseRuntimeExecutionContextParamsContext Context);
    public sealed record DTResponseRuntimeExecutionContextParamsContext(int Id, string Origin, string Name, string UniqueId, DTResponseRuntimeExecutionContextParamsContextAuxData AuxData);
    public sealed record DTResponseRuntimeExecutionContextParamsContextAuxData(bool IsDefault, string Type, string FrameId);
}

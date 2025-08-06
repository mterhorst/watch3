namespace Watch3.Models.Obs
{
    public sealed record ObsWsIdentify(int RpcVersion, string? Authentication, int EventSubscriptions);
}
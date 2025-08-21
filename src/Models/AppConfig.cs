namespace Watch3.Models
{
    public sealed record AppConfig(bool IsClient, string HostedHost, string ClientHost);
}

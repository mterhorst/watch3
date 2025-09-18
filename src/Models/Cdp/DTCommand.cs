namespace Watch3.Models.Cdp
{
    public sealed record DTCommand(int Id, string Method, string? SessionId = null, string? TargetId = null, object? Params = null);
}

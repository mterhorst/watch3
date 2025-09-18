namespace Watch3.Models.Cdp
{
    public sealed record DTCommandSetCookie(string Name, string Value, string? Url = null, string? Domain = null,
        bool? Secure = null, bool? HttpOnly = null);

}

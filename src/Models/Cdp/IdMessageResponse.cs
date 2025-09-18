using System.Text.Json.Nodes;

namespace Watch3.Models.Cdp
{
    public sealed record IdMessageResponse(int Id, string? SessionId, JsonObject Json, IEnumerable<string> Errors);
}

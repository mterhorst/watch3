using System.Text.Json.Nodes;

namespace Watch3.Models.Cdp
{
    public sealed record MethodMessageResponse(string Method, string? SessionId, JsonObject Json, IEnumerable<string> Errors);
}

using System.Text.Json.Serialization;

namespace Watch3.Models.Cdp
{
    public sealed record DTJsonVersionResponse(string Browser,
        [property: JsonPropertyName("Protocol-Version")] string ProtocolVersion,
        [property: JsonPropertyName("User-Agent")] string UserAgent,
        [property: JsonPropertyName("V8-Version")] string V8Version,
        [property: JsonPropertyName("WebKit-Version")] string WebKitVersion,
        string WebSocketDebuggerUrl);

}

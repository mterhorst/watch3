using System.Text.Json.Nodes;

namespace Watch3.Models.Cdp
{
    public sealed record DTResponsePageFrameNavigated(string Method, string SessionId, DTResponsePageFrameNavigatedParams Params);
    public sealed record DTResponsePageFrameNavigatedParams(DTResponsePageFrameNavigatedParamsFrame Frame, string Type);
    public sealed record DTResponsePageFrameNavigatedParamsFrame(string Id, string LoaderId, string Url, string? UrlFragment, string DomainAndRegistry,
        string SecurityOrigin, string MimeType, DTResponsePageFrameNavigatedParamsFrameAdFrameStatus AdFrameStatus, string SecureContextType,
        string CrossOriginIsolatedContextType, JsonArray GatedAPIFeatures);
    public sealed record DTResponsePageFrameNavigatedParamsFrameAdFrameStatus(string AdFrameType);
}

using System.Text.Json.Nodes;

namespace Watch3.Models.Cdp
{
    public sealed record DTResponsePageGetFrameTree(int Id, string sessionId, DTResponsePageGetFrameTreeResult Result);
    public sealed record DTResponsePageGetFrameTreeResult(DTResponsePageGetFrameTreeResultFrameTree FrameTree);
    public sealed record DTResponsePageGetFrameTreeResultFrameTree(DTResponsePageGetFrameTreeResultFrameTreeFrame Frame,
    IEnumerable<DTResponsePageGetFrameTreeResultFrameTreeFrame> ChildFrames);
    public sealed record DTResponsePageGetFrameTreeResultFrameTreeFrame(string Id, int ExecutionContextId, string LoaderId, string Url, string DomainAndRegistry,
    string SecurityOrigin, string MimeType, DTResponsePageGetFrameTreeResultFrameTreeFrameAdFrameStatus AdFrameStatus, string SecureContextType, string CrossOriginIsolatedContextType, JsonArray GatedAPIFeatures);
    public sealed record DTResponsePageGetFrameTreeResultFrameTreeFrameAdFrameStatus(string AdFrameType);
}

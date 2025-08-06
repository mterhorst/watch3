using System.Text.Json.Nodes;

namespace Watch3.Models.Obs
{
    public sealed record ObsWsEvent(string EventType, int EventIntent, JsonObject EventData);
}
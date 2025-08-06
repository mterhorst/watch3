using System.Text.Json.Nodes;

namespace Watch3.Models.Obs
{
    public sealed record ObsWsRoot(int Op, JsonNode D);
}
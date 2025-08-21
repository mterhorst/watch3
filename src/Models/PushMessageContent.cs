using System.Text.Json.Nodes;

namespace Watch3.Models
{
    public sealed record PushPayload(CommandType Type, JsonObject Body);

    public enum CommandType
    {
        StartStream
    }
}

using System.Text.Json.Nodes;

namespace Watch3.Models
{
    public sealed record PushPayload(Guid Id, PushSubscription Subscription, CommandType Type, JsonNode Data);

    public enum CommandType
    {
        GetOffer,
        GetAnswer,
        StartStream,
        StopStream
    }
}

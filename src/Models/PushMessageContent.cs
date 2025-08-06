using Lib.Net.Http.WebPush;
using System.Text.Json.Nodes;

namespace Watch3.Models
{
    //public sealed record PushMessageContent(PushSubscription Subscription, PushPayload Payload);

    public sealed record PushPayload(CommandType Type, JsonObject Body);

    public enum CommandType
    {
        StartStream
    }
}

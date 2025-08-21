namespace Watch3.Models
{
    public sealed record PushSubscription(string Endpoint, DateTimeOffset? ExpirationTime, PushSubscriptionkey Keys);
    public sealed record PushSubscriptionkey(string P256dh, string Auth);
}

using System.Security.Cryptography;
using System.Text;

namespace Watch3.Models
{
    public sealed record PushSubscription(string Endpoint, DateTimeOffset? ExpirationTime, PushSubscriptionkey Keys)
    {
        public SubscriptionInfo Info => new
        (
            Id: GetHashCode()
        );

        public override int GetHashCode()
        {
            var input = $"{nameof(PushSubscription)} {{ {nameof(Endpoint)} = {Endpoint}, {nameof(ExpirationTime)} = {ExpirationTime}, {nameof(Keys)} = {Keys} }}";
            var bytes = Encoding.UTF8.GetBytes(input);
            var hashBytes = SHA256.HashData(bytes);
            return BitConverter.ToInt32(hashBytes, 0);
        }
    }
    public sealed record PushSubscriptionkey(string P256dh, string Auth);

    public sealed record SubscriptionInfo(int Id);
}

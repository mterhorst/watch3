namespace Watch3.Models
{
    public sealed record RTCSessionDescriptionInit(string Sdp, RTCSdpType Type);

    public enum RTCSdpType
    {
        Answer,
        Offer,
        Pranswer,
        Rollback
    }
}

namespace Watch3.Models.Cdp
{
    public sealed record DTResponsePageGetNavigationHistory(int Id, string sessionId, DTResponsePageGetNavigationHistoryResult Result);
    public sealed record DTResponsePageGetNavigationHistoryResult(int CurrentIndex, IList<DTResponsePageGetNavigationHistoryResultEntry> Entries);
    public sealed record DTResponsePageGetNavigationHistoryResultEntry(int Id, string Url, string UserTypedURL, string Title, string TransitionType);
}

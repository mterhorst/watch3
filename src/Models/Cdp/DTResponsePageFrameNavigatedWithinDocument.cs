namespace Watch3.Models.Cdp
{
    public sealed record DTResponsePageFrameNavigatedWithinDocument(string Method, string SessionId, DTResponsePageFrameNavigatedWithinDocumentParams Params);
    public sealed record DTResponsePageFrameNavigatedWithinDocumentParams(string FrameId, string Url);
}

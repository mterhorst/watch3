namespace Watch3.Models.Obs
{
    public sealed record ObsWsGetRecordStatusResponse(
        bool OutputActive,
        bool OutputPaused,
        string OutputTimecode,
        int OutputDuration,
        int OutputBytes);
}
namespace Watch3.Models.Obs
{
    public sealed record ObsWsGetStreamStatusResponse(
        bool OutputActive,
        bool OutputReconnecting,
        string OutputTimecode,
        int OutputDuration,
        int OutputCongestion,
        int OutputBytes,
        int OutputSkippedFrames,
        int OutputTotalFrames);
}
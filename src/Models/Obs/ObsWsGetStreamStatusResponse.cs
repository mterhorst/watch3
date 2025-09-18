namespace Watch3.Models.Obs
{
    public sealed record ObsWsGetStreamStatusResponse(
        bool OutputActive,
        int OutputBytes,
        double OutputCongestion,
        int OutputDuration,
        bool OutputReconnecting,
        int OutputSkippedFrames,
        string OutputTimecode,
        int OutputTotalFrames);
}
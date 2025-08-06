namespace Watch3.Services
{
    public sealed class RollingFileService : BackgroundService
    {
        private readonly ObsWebsocketService _obsWs;
        private readonly HelperService _helper;
        private readonly ILogger<RollingFileService> _logger;

        private Timer? _timer;

        public RollingFileService(ObsWebsocketService obsWs, HelperService helper, ILogger<RollingFileService> logger)
        {
            _obsWs = obsWs;
            _helper = helper;
            _logger = logger;
        }

        //[DebuggerStepThrough]
        protected override Task ExecuteAsync(CancellationToken bgToken)
        {
            var now = DateTime.Now;
            var nextMidnight = now.Date.AddDays(1);
            var initialDelay = nextMidnight - now;

            _timer = new Timer(async _ =>
            {
                if (!_helper.IsObsRunning)
                {
                    await _helper.LaunchObs();
                }

                try
                {
                    await using var wsSession = await _obsWs.GetSession();
                    await wsSession.SplitRecordFile(bgToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error running scheduled task");
                }
            }, state: null, initialDelay, TimeSpan.FromDays(1));

            return Task.CompletedTask;
        }

        public override void Dispose()
        {
            _timer?.Dispose();
        }
    }
}

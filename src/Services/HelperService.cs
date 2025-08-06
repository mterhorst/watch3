using System.Diagnostics;
using Watch3.Models.Obs;

namespace Watch3.Services
{
    public sealed class HelperService
    {
        public static readonly bool IsClient = OperatingSystem.IsWindows();

        public readonly SemaphoreSlim ObsLock = new(initialCount: 1, maxCount: 1);

        public string HostedHost => _configuration.GetValue<string>("HostedHost") ?? throw new ArgumentNullException("__HostedHost");
        public string ClientHost => _configuration.GetValue<string>("ClientHost") ?? throw new ArgumentNullException("__ClientHost");

        public readonly Uri ObsUri = new UriBuilder("ws", "localhost", 8081).Uri;

        public ObsConfig ObsConfig => _configuration.GetSection("ObsConfig").Get<ObsConfig>() ?? throw new KeyNotFoundException("ObsConfig not found.");

        private readonly IConfiguration _configuration;

        public HelperService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public bool IsObsRunning
        {
            get
            {
                var processes = Process.GetProcessesByName(ObsConfig.ExeName);
                int i = 0;
                for (; i < processes.Length; i++)
                {
                    using var proc = processes[i];
                }
                return i > 0;
            }
        }

        public async Task<bool> LaunchObs(CancellationToken token = default)
        {
            if (IsObsRunning)
                return false;

            await ObsLock.WaitAsync(token);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    WorkingDirectory = ObsConfig.ObsDirectory,
                    FileName = $"{ObsConfig.ExeName}.exe",
                    UseShellExecute = true
                };
                var process = Process.Start(startInfo)!;
                process.WaitForInputIdle(Timeout.Infinite);
            }
            finally
            {
                ObsLock.Release();
            }

            return true;
        }
    }
}

using System.Diagnostics;
using System.Runtime.InteropServices;
using Watch3.Models;
using Watch3.Models.Obs;

namespace Watch3.Services
{
    public sealed class HelperService
    {
        public readonly SemaphoreSlim ObsLock = new(initialCount: 1, maxCount: 1);
        public readonly Uri ObsUri = new UriBuilder("ws", "localhost", 8081).Uri;
        
        public AppConfig AppConfig => _configuration.GetSection("AppConfig").Get<AppConfig>() ?? throw new KeyNotFoundException("AppConfig not found.");
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

            var isWindows = OperatingSystem.IsWindows();
            var exeName = isWindows ? $"{ObsConfig.ExeName}.exe" : ObsConfig.ExeName;
            
            await ObsLock.WaitAsync(token);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    WorkingDirectory = ObsConfig.ObsDirectory,
                    FileName = Path.Combine(ObsConfig.ObsDirectory, exeName)
                };
                var process = Process.Start(startInfo)!;

                if (isWindows)
                {
                    process.WaitForInputIdle(Timeout.Infinite);
                }
            }
            finally
            {
                ObsLock.Release();
            }

            return true;
        }

        public void LaunchBrowser(Uri url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo("cmd", $"/c start {url}") { CreateNoWindow = true });
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    Process.Start("xdg-open", url.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error launching browser: {ex.Message}");
            }
        }
    }
}

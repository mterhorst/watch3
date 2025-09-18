using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Net.WebSockets;
using Watch3.Models;
using Watch3.Models.Cdp;

namespace Watch3.Cdp
{
    public sealed class CdpBrowser : IAsyncDisposable
    {
        public event EventHandler<Exception>? OnConnectionClosed;

        private static event EventHandler<Exception>? _onConnectionClosed;

        private readonly AppConfig _config;
        private readonly IServiceProvider _services;
        private readonly IHostApplicationLifetime _lifetime;

        public CdpBrowser(AppConfig config, IServiceProvider services, IHostApplicationLifetime lifetime)
        {
            _config = config;
            _services = services;
            _lifetime = lifetime;
        }

        public async Task<CdpBrowserSession> Launch(bool headless, string profile)
        {
            var port = GetAvailablePort();
            StartBrowser(port, headless, profile);
            var session = await WaitForSession(port);
            var connection = await Connect(new Uri(session.WebSocketDebuggerUrl));
            var browserSession = ActivatorUtilities.CreateInstance<CdpBrowserSession>(_services, connection, _lifetime.ApplicationStopping);
            return browserSession;
        }

        public async ValueTask DisposeAsync()
        {
            _onConnectionClosed -= RaiseOnConnectionClosed;
            await Task.Delay(TimeSpan.FromSeconds(1));
        }

        private async Task<CdpConnection> Connect(Uri url)
        {
            var client = new ClientWebSocket();
            client.Options.KeepAliveInterval = TimeSpan.Zero;
            await client.ConnectAsync(url, _lifetime.ApplicationStopping);

            var connection = ActivatorUtilities.CreateInstance<CdpConnection>(_services, client);

            _ = connection.StartReciever().ContinueWith((task) =>
            {
                connection.Dispose();
                _onConnectionClosed?.Invoke(null, task.Exception!.GetBaseException());
            }, TaskContinuationOptions.OnlyOnFaulted);

            return connection;
        }

        private void StartBrowser(int port, bool headless, string profile)
        {
            var args = GetArguments(port, headless, profile).ToList();

            var startInfo = new ProcessStartInfo
            {
                FileName = _config.BrowserExecutable,
                Arguments = string.Join(' ', args),
                UseShellExecute = true
            };

            var process = new Process
            {
                StartInfo = startInfo
            };

            process.Start();

            _lifetime.ApplicationStopping.Register(() =>
            {
                process.Kill();
            });

            _ = Task.Run(async () =>
            {
                await process.WaitForExitAsync();

                _lifetime.StopApplication();
            });
        }

        private async Task<DTJsonVersionResponse> WaitForSession(int port)
        {
            const int tryCount = 4;

            using var http = new HttpClient();

            for (int i = 0; i < tryCount; i++)
            {
                try
                {
                    return (await http.GetFromJsonAsync($"http://127.0.0.1:{port}/json/version", Json.Default.DTJsonVersionResponse))!;
                }
                catch { }
            }

            throw new TimeoutException();
        }

        private IEnumerable<string> GetArguments(int port, bool headless, string profile)
        {
            var profileDir = new DirectoryInfo(Path.Combine(AppContext.BaseDirectory, "data", profile));

            yield return "about:blank";
            yield return $"--remote-debugging-port={port}";
            yield return "--no-first-run";
            yield return "--enable-automation";
            yield return "--disable-prompt-on-repost";
            yield return "--disable-extensions";
            yield return "--disable-component-update";
            yield return "--disable-sync";
            yield return "--disable-features=Translate,BackForwardCache,AcceptCHFrame,MediaRouter,OptimizationHints";
            yield return "--disable-default-apps";
            yield return "--disable-infobars";
            yield return "--disable-client-side-phishing-detection";
            yield return "--disable-gpu";
            yield return "--disable-breakpad";
            yield return $"--profile-directory={profile}";
            yield return "--no-profiles";
            yield return $"""--user-data-dir="{profileDir.FullName}" """;

            if (headless)
                yield return "--headless";
        }

        private static int GetAvailablePort()
        {
            using var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            socket.Bind(new IPEndPoint(IPAddress.Any, 0));

            if (socket.LocalEndPoint is not IPEndPoint endPoint)
                throw new InvalidOperationException();

            return endPoint.Port;
        }

        private void RaiseOnConnectionClosed(object? sender, Exception e) => OnConnectionClosed?.Invoke(sender, e);
    }
}

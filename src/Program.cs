using Watch3.Cdp;
using Watch3.Http;
using Watch3.Models;
using Watch3.Services;

namespace Watch3
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateSlimBuilder(args);

            builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: false);
            builder.Configuration.AddJsonFile("appsettings.Client.json", optional: true, reloadOnChange: false);

            builder.Configuration.AddEnvironmentVariables();

            var appConfig = builder.Configuration.GetSection("AppConfig").Get<AppConfig>() ?? throw new KeyNotFoundException("AppConfig not found.");

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, Json.Default);
            });

            builder.Logging.AddConsole();

            builder.Services.AddTransient<ObsWebsocketSession>();
            builder.Services.AddTransient<HostedHttpHandler>();
            builder.Services.AddSingleton(appConfig);
            builder.Services.AddSingleton<HelperService>();
            builder.Services.AddSingleton<ObsWebsocketService>();
            builder.Services.AddSingleton<CdpBrowser>();
            builder.Services.AddHttpClient<HostedHttp>().AddHttpMessageHandler<HostedHttpHandler>();
            builder.Services.AddHttpClient<VapidHttp>();

            builder.Services.AddKeyedSingleton("PushClient",
            builder.Configuration.GetSection("PushClient").Get<PushServiceConfig>() ?? throw new KeyNotFoundException("PushClient not found."));

            builder.Services.AddKeyedSingleton("PushUser",
            builder.Configuration.GetSection("PushUser").Get<PushServiceConfig>() ?? throw new KeyNotFoundException("PushUser not found."));

            var app = builder.Build();

            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.UseWebSockets();

            Routes.RegisterWhipRoutes(app.MapGroup("/whip"));
            Routes.RegisterApiRoutes(app.MapGroup("/api"));

            app.Lifetime.ApplicationStarted.Register(async () =>
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Application started in {Environment} environment. Client mode: {IsClient}", builder.Environment.EnvironmentName, appConfig.IsClient);

                if (appConfig.IsClient)
                {
                    var helper = app.Services.GetRequiredService<HelperService>();
                    var browser = app.Services.GetRequiredService<CdpBrowser>();

                    var url = new UriBuilder(helper.AppConfig.ClientHost)
                    {
                        Path = "/"
                    }.Uri;

                    var browserSession = await browser.Launch(headless: false, "ClientProfile");

                    var appPage = (await browserSession.GetPages()).First();
                    await appPage.Navigate(url.ToString());
                    await appPage.SetCookie(nameof(PushName), Enum.GetName(PushName.PushClient)!, url.Authority);

                    if (app.Environment.IsDevelopment())
                    {
                        var browserSession2 = await browser.Launch(headless: false, "UserProfile");

                        var appPage2 = (await browserSession2.GetPages()).First();
                        await appPage2.Navigate(url.ToString());
                        await appPage2.SetCookie(nameof(PushName), Enum.GetName(PushName.PushUser)!, url.Authority);
                    }

                    //if (!helper.IsObsRunning)
                    //{
                    //    await helper.LaunchObs();
                    //}
                }
            });

            app.Run();
        }
    }
}
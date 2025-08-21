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
            var pushServiceConfig = builder.Configuration.GetSection("PushServiceConfig").Get<PushServiceConfig>() ?? throw new KeyNotFoundException("PushServiceConfig not found.");

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, Json.JsonAppContext);
            });

            builder.Logging.AddConsole();

            builder.Services.AddTransient<ObsWebsocketSession>();
            builder.Services.AddTransient<HostedHttpHandler>();
            builder.Services.AddSingleton(appConfig);
            builder.Services.AddSingleton(pushServiceConfig);
            builder.Services.AddSingleton<HelperService>();
            builder.Services.AddSingleton<ObsWebsocketService>();
            builder.Services.AddHttpClient<HostedHttp>().AddHttpMessageHandler<HostedHttpHandler>();
            builder.Services.AddHttpClient<VapidHttp>();

            if (appConfig.IsClient)
            {
                builder.Services.AddHostedService<RollingFileService>();
            }

            builder.Services.AddSignalR();

            var app = builder.Build();

            app.UseStaticFiles();
            app.UseWebSockets();

            app.MapHub<SignalingHub>("/signal");

            Routes.RegisterControlRoutes(app.MapGroup("/control"));
            Routes.RegisterWhipRoutes(app.MapGroup("/whip"));
            Routes.RegisterApiRoutes(app.MapGroup("/api"));

            app.Lifetime.ApplicationStarted.Register(async () =>
            {
                var logger = app.Services.GetRequiredService<ILogger<Program>>();
                logger.LogInformation("Application started in {Environment} environment. Client mode: {IsClient}",
                    builder.Environment.EnvironmentName,
                    appConfig.IsClient);

                if (appConfig.IsClient)
                {
                    var helper = app.Services.GetRequiredService<HelperService>();
                    if (!helper.IsObsRunning)
                    {
                        await helper.LaunchObs();
                    }
                }
            });

            app.Run();
        }
    }
}
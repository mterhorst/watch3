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

            if (HelperService.IsClient)
            {
                builder.Configuration.AddJsonFile("appsettings.Client.json", optional: false, reloadOnChange: false);
            }

            builder.Services.ConfigureHttpJsonOptions(options =>
            {
                options.SerializerOptions.TypeInfoResolverChain.Insert(0, Json.JsonAppContext);
            });

            //builder.Services.AddCors(options =>
            //{
            //    options.AddPolicy("AllowCors", x => x.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod());
            //});

            builder.Logging.AddConsole();

            builder.Services.AddTransient<ObsWebsocketSession>();
            builder.Services.AddSingleton<HelperService>();
            builder.Services.AddSingleton<ObsWebsocketService>();
            builder.Services.AddSingleton<HostedHttpHandler>();
            builder.Services.AddHttpClient<HostedHttp>().AddHttpMessageHandler<HostedHttpHandler>();

            if (HelperService.IsClient)
            {
                builder.Services.AddHostedService<RollingFileService>();
            }

            builder.Services.AddSignalR();

            builder.Services.AddPushServiceClient(options =>
            {
                var pushServiceConfig = builder.Configuration.GetSection("PushServiceConfig").Get<PushServiceConfig>() 
                    ?? throw new KeyNotFoundException("PushServiceConfig not found.");
                options.Subject = pushServiceConfig.Subject;
                options.PublicKey = pushServiceConfig.PublicKey;
                options.PrivateKey = pushServiceConfig.PrivateKey;
            });

            //builder.WebHost.UseKestrelHttpsConfiguration();

            var app = builder.Build();

            app.UseStaticFiles();
            app.UseWebSockets();

            app.MapHub<SignalingHub>("/signal");

            Routes.RegisterControlRoutes(app.MapGroup("/control"));
            Routes.RegisterWhipRoutes(app.MapGroup("/whip"));
            Routes.RegisterApiRoutes(app.MapGroup("/api"));

            app.Lifetime.ApplicationStarted.Register(async () =>
            {
                if (HelperService.IsClient)
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
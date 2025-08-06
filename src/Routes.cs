using Lib.Net.Http.WebPush;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Watch3.Models;
using Watch3.Services;

namespace Watch3
{
    public static class Routes
    {
        public static Dictionary<string, TaskCompletionSource<string>> AnswerWaiters = [];
        private static PushSubscription? pushSubscription;

        public static void RegisterControlRoutes(RouteGroupBuilder controlApi)
        {
            controlApi.MapPost("register", (
                [FromBody] PushSubscription subscription,
                [FromServices] PushServiceClient client,
                [FromServices] ILogger<Program> logger) =>
            {
                ArgumentNullException.ThrowIfNull(subscription);

                logger.LogInformation(subscription.ToString());
                pushSubscription = subscription;
            });

            controlApi.MapPost("push", async ([FromServices] PushServiceClient client, [FromBody] PushPayload push) =>
            {
                if (pushSubscription is null)
                {
                    return Results.BadRequest();
                }

                var payload = JsonSerializer.Serialize(push, Json.JsonAppContext.PushPayload);
                await client.RequestPushMessageDeliveryAsync(pushSubscription, new PushMessage(payload));

                return Results.Ok();
            });
        }

        public static void RegisterWhipRoutes(RouteGroupBuilder whipApi)
        {
            whipApi.MapPost("/", async (
                [FromServices] IHubContext<SignalingHub> hubContext,
                [FromServices] HelperService helper,
                [FromServices] ILogger<Program> logger,
                HttpContext context, CancellationToken token) =>
            {
                var room = "room1";

                Console.WriteLine($"{context.Connection.RemoteIpAddress}:{context.Connection.RemotePort}");

                using var streamReader = new HttpRequestStreamReader(context.Request.Body, Encoding.UTF8);
                var sdpOffer = await streamReader.ReadToEndAsync();

                var tcs = new TaskCompletionSource<string>();
                AnswerWaiters[room] = tcs;

                await hubContext.Clients.Group(room).SendAsync("ReceiveSignal", sdpOffer, cancellationToken: token);

                var answerSdp = await tcs.Task;

                var location = new UriBuilder(helper.ClientHost)
                {
                    Path = $"whip",
                }.Uri;

                logger.LogInformation(location.ToString());

                context.Response.StatusCode = 201;
                context.Response.ContentType = MediaTypeHeaderValue.Parse("application/sdp").ToString();
                context.Response.Headers.Location = location.ToString();
                await context.Response.WriteAsync(answerSdp, cancellationToken: token);
            });

            whipApi.MapDelete("/{sessionId}", async ([FromRoute] Guid sessionId, HttpContext context) =>
            {
                return Results.Ok();
            });
        }

        public static void RegisterApiRoutes(RouteGroupBuilder webApi)
        {
            webApi.MapPost("register_subscription", async (
                [FromBody] PushSubscription subscription,
                [FromServices] HostedHttp http,
                CancellationToken token) =>
            {
                await http.RegisterHostedNotifications(subscription, token);
                return Results.Ok();
            });

            webApi.MapPost("stream/start", async ([FromServices] ObsWebsocketService obsWs, CancellationToken token) =>
            {
                await using var peerConnection = await obsWs.GetSession();
                await peerConnection.StartStream(token);
                return Results.Ok();
            });
        }

    }
}
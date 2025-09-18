using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.WebUtilities;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Watch3.Http;
using Watch3.Models;
using Watch3.Services;

namespace Watch3
{
    public static class Routes
    {
        private static PushSubscription? _clientPushSubscription;

        private static readonly Channel<string> _offerChannel =
            Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });

        private static readonly Channel<string> _answerChannel =
            Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = false,
                SingleWriter = false
            });

        public static void RegisterWhipRoutes(RouteGroupBuilder whipApi)
        {
            whipApi.MapPost("/offer", async ([FromServices] ObsWebsocketService obsWs, CancellationToken token) =>
            {
                var session = await obsWs.GetSession();

                var streamStatus = await session.GetStreamStatus(token);

                if (streamStatus.OutputActive)
                {
                    return Results.BadRequest(new ErrorResponse(Code: 1, Error: "Failed to load stream. Stream already started."));
                }

                await session.StartStream(token);

                if (await _offerChannel.Reader.WaitToReadAsync(token))
                {
                    var sdpOffer = await _offerChannel.Reader.ReadAsync(token);
                    return Results.Content(sdpOffer, "application/sdp");
                }

                return Results.Ok();
            });

            whipApi.MapPost("/answer", async (HttpRequest request, CancellationToken token) =>
            {
                using var streamReader = new HttpRequestStreamReader(request.Body, Encoding.UTF8);
                var sdpAnswer = await streamReader.ReadToEndAsync();

                await _answerChannel.Writer.WriteAsync(sdpAnswer, token);

                return Results.Ok();
            });

            whipApi.MapPost("/", async ([FromServices] HelperService helper,
                                        [FromServices] ILogger<Program> logger,
                                        HttpContext context,
                                        CancellationToken token) =>
            {
                using var streamReader = new HttpRequestStreamReader(context.Request.Body, Encoding.UTF8);
                var sdpOffer = await streamReader.ReadToEndAsync();

                await _offerChannel.Writer.WriteAsync(sdpOffer, token);

                var reader = _answerChannel.Reader;
                string answerSdp;
                try
                {
                    if (await reader.WaitToReadAsync(token))
                    {
                        answerSdp = await reader.ReadAsync(token);
                    }
                    else
                    {
                        return Results.BadRequest(new ErrorResponse(Error: "No answer available."));
                    }
                }
                catch (OperationCanceledException)
                {
                    logger.LogInformation("Canceled while waiting for SDP answer.");
                    return Results.BadRequest();
                }

                var location = new UriBuilder(helper.AppConfig.ClientHost)
                {
                    Path = $"whip",
                }.Uri;

                context.Response.Headers.Location = location.ToString();
                return Results.Content(
                    content: answerSdp,
                    contentType: MediaTypeHeaderValue.Parse("application/sdp").ToString(),
                    statusCode: StatusCodes.Status201Created);
            });

            whipApi.MapDelete("/", (HttpContext context) =>
            {
                return Results.Ok();
            });
        }

        public static void RegisterApiRoutes(RouteGroupBuilder webApi)
        {
            webApi.MapGet("/vapid_config", ([FromServices] IServiceProvider services, [FromServices] ILogger<Program> logger, HttpRequest request) =>
            {
                if (request.Cookies.TryGetValue(nameof(PushName), out var name) && Enum.TryParse<PushName>(name, out var pushName))
                {
                    logger.LogInformation("Found PushName {pushName}", pushName);
                }
                else
                {
                    logger.LogInformation("Defaulting PushName to {PushName}", PushName.PushUser);
                    pushName = PushName.PushUser;
                }

                logger.LogInformation("Gettting vapid config {pushName}", pushName);

                var pushConfig = services.GetKeyedService<PushServiceConfig>(Enum.GetName(pushName));
                if (pushConfig is null)
                {
                    return Results.BadRequest();
                }

                return Results.Json(pushConfig with { PrivateKey = "" }, Json.Default.PushServiceConfig);
            });

            webApi.MapPost("register_subscription", async ([FromBody] PushSubscription subscription,
                                                           [FromServices] HostedHttp http,
                                                           [FromServices] IWebHostEnvironment environment,
                                                           [FromServices] AppConfig config,
                                                           [FromServices] ILogger<Program> logger,
                                                           HttpContext context,
                                                           CancellationToken token) =>
            {


                if (subscription is null)
                {
                    return Results.BadRequest();
                }

                if (context.Request.Cookies.TryGetValue(nameof(PushName), out var name) && Enum.TryParse<PushName>(name, out var pushName))
                {
                    logger.LogInformation("Found PushName {pushName}", pushName);
                }
                else
                {
                    logger.LogInformation("Defaulting PushName to {PushName}", PushName.PushUser);
                    pushName = PushName.PushUser;
                }

                logger.LogInformation("Attempting to register {pushName} notifications for subscription: {Subscription}", pushName, subscription);

                if (pushName == PushName.PushClient)
                {
                    if (!config.IsClient || environment.IsDevelopment())
                    {
                        _clientPushSubscription = subscription;
                    }
                    else
                    {
                        try
                        {
                            await http.RegisterHostedNotifications(subscription, token);
                            logger.LogInformation("Successfully registered hosted notifications for subscription: {Subscription}", subscription);
                        }
                        catch (Exception ex)
                        {
                            logger.LogError(ex, "Failed to register hosted notifications for subscription: {Subscription}", subscription);
                            return Results.BadRequest();
                        }
                    }
                }

                context.Response.Cookies.Append("SubscriptionId", subscription.Info.Id.ToString());
                return Results.Json(subscription.Info, Json.Default.SubscriptionInfo);
            });

            webApi.MapPost("push", async ([FromBody] PushPayload payload,
                                          [FromServices] VapidHttp client,
                                          [FromKeyedServices("PushClient")] PushServiceConfig pushClient,
                                          [FromServices] ILogger<Program> logger,
                                          CancellationToken token) =>
            {
                if (_clientPushSubscription is null)
                {
                    logger.LogInformation("No client subscription found.");
                    return Results.BadRequest(new ErrorResponse(Error: "No client subscription found."));
                }

                await client.RequestPushMessageDelivery(pushClient, _clientPushSubscription, payload, token);

                return Results.Ok();
            });

            webApi.MapPost("accept_push", async ([FromBody] PushPayload payload,
                                                 [FromKeyedServices("PushUser")] PushServiceConfig pushUser,
                                                 [FromServices] VapidHttp client,
                                                 [FromServices] ObsWebsocketService obsWs,
                                                 CancellationToken token) =>
            {
                var updatedPayload = payload with
                {
                    Data = new JsonObject()
                };

                if (payload.Type == CommandType.StartStream)
                {
                    var peerConnection = await obsWs.GetSession();
                    await peerConnection.StartStream(token);
                }
                if (payload.Type == CommandType.StopStream)
                {
                    var peerConnection = await obsWs.GetSession();
                    await peerConnection.StopStream(token);
                }
                if (payload.Type == CommandType.GetOffer)
                {
                    var session = await obsWs.GetSession();

                    var streamStatus = await session.GetStreamStatus(token);

                    if (streamStatus.OutputActive)
                    {
                        updatedPayload = payload with
                        {
                            Data = JsonSerializer.SerializeToNode(new ErrorResponse
                            (
                                Code: 1,
                                Error: "Failed to load stream. Stream already started."
                            ), Json.Default.ErrorResponse)!
                        };
                    }

                    await session.StartStream(token);

                    if (await _offerChannel.Reader.WaitToReadAsync(token))
                    {
                        var sdpOffer = await _offerChannel.Reader.ReadAsync(token);
                        updatedPayload = payload with
                        {
                            Data = JsonSerializer.SerializeToNode(new RTCSessionDescriptionInit
                            (
                                Sdp: sdpOffer,
                                Type: RTCSdpType.Offer
                            ), Json.Default.RTCSessionDescriptionInit)!
                        };
                    }
                }
                if (payload.Type == CommandType.GetAnswer)
                {
                    if (payload.Data["sdp"]?.GetValue<string>() is { } sdpAnswer)
                    {
                        await _answerChannel.Writer.WriteAsync(sdpAnswer, token);
                    }
                }

                await client.RequestPushMessageDelivery(pushUser, payload.Subscription, updatedPayload, token);

                return Results.Ok();
            });
        }
    }

    public enum PushName
    {
        PushClient,
        PushUser
    }
}
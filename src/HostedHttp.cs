using System.Net;
using System.Net.Http.Headers;
using System.Net.Mime;
using System.Text;
using Watch3.Services;
using Watch3.Models;
using Lib.Net.Http.WebPush;

namespace Watch3
{
    public sealed class HostedHttp
    {
        private readonly HttpClient _client;
        private readonly HelperService _helper;

        public HostedHttp(HttpClient client, HelperService helper)
        {
            _client = client;
            _helper = helper;
        }

        public async Task RegisterHostedNotifications(PushSubscription subscription, CancellationToken token = default)
        {
            var url = new UriBuilder(_helper.HostedHost)
            {
                Path = "control/register"
            }.Uri;

            await _client.PostAsJsonAsync(url, subscription, Json.JsonAppContext.PushSubscription, token);
        }
    }

    public sealed class HostedHttpHandler : DelegatingHandler
    {
        private static EntraIdTokenResponse? entraIdTokenResponse;
        private static DateTime? issuedAt;

        private readonly EntraId _entraId;
        private readonly ILogger<HostedHttpHandler> _logger;

        public HostedHttpHandler(IConfiguration configuration, ILogger<HostedHttpHandler> logger)
        {
            _entraId = configuration.GetSection("EntraID").Get<EntraId>() ?? throw new KeyNotFoundException("EntraID not found.");
            _logger = logger;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            if (entraIdTokenResponse is null ||
                issuedAt is null ||
                DateTime.UtcNow > issuedAt.Value.AddSeconds(entraIdTokenResponse.ExpiresIn))
            {
                try
                {
                    using var client = new HttpClient();

                    using var tokenResponse = await client.SendAsync(new HttpRequestMessage
                    {
                        Method = HttpMethod.Post,
                        RequestUri = new UriBuilder("https://login.microsoftonline.com")
                        {
                            Path = $"{_entraId.tenantId}/oauth2/v2.0/token"
                        }.Uri,
                        Content = new FormUrlEncodedContent(
                        [
                            new KeyValuePair<string, string>("client_id", _entraId.ClientId),
                            new KeyValuePair<string, string>("client_secret", _entraId.ClientSecret),
                            new KeyValuePair<string, string>("scope", _entraId.Scope),
                            new KeyValuePair<string, string>("grant_type", "client_credentials")
                        ])
                    }, token);

                    if (!tokenResponse.IsSuccessStatusCode)
                    {
                        throw new HttpRequestException(await tokenResponse.Content.ReadAsStringAsync(token), null, tokenResponse.StatusCode);
                    }

                    entraIdTokenResponse = (await tokenResponse.Content.ReadFromJsonAsync(Json.JsonAppContext.EntraIdTokenResponse, token))!;
                    issuedAt = DateTime.UtcNow;
                }
                catch (HttpRequestException hre) when (hre.StatusCode == HttpStatusCode.Unauthorized)
                {
                    _logger.LogError(hre, "Authentication request failed.");
                    return new HttpResponseMessage
                    {
                        StatusCode = hre.StatusCode.Value,
                        Content = new StringContent(hre.Message, Encoding.UTF8, MediaTypeNames.Application.Json)
                    };
                }
                catch
                {
                    throw;
                }
            }

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", entraIdTokenResponse.AccessToken);

            var response = await base.SendAsync(request, token);

            response.EnsureSuccessStatusCode();

            return response;
        }

    }
}
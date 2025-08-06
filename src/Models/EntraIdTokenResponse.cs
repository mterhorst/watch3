using System.Text.Json.Serialization;

namespace Watch3.Models
{
    public sealed record EntraIdTokenResponse([property: JsonPropertyName("token_type")] string TokenType,
                                              [property: JsonPropertyName("expires_in")] int ExpiresIn,
                                              [property: JsonPropertyName("ext_expires_in")] int ExtExpiresIn,
                                              [property: JsonPropertyName("access_token")] string AccessToken);
}

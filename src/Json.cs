using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Watch3.Models;
using Watch3.Models.Obs;

namespace Watch3
{
    public sealed class Json
    {
        public static readonly AppJsonSerializerContext JsonAppContext;

        public static readonly JsonSerializerOptions JsonOptions;

        static Json()
        {
            var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
            {
                WriteIndented = true,
                Converters =
                {
                    new JsonStringEnumConverter(JsonNamingPolicy.CamelCase)
                }
            };
            JsonOptions = options;

            JsonAppContext = new(options);
        }
    }

    [JsonSerializable(typeof(JsonObject))]
    [JsonSerializable(typeof(JsonArray))]
    [JsonSerializable(typeof(double))]
    [JsonSerializable(typeof(List<List<JsonNode>>))]
    [JsonSerializable(typeof(List<Array>))]

    [JsonSerializable(typeof(PushSubscription))]
    [JsonSerializable(typeof(PushPayload))]
    [JsonSerializable(typeof(EntraIdTokenResponse))]
    [JsonSerializable(typeof(EntraId))]


    [JsonSerializable(typeof(ObsWsRoot))]
    [JsonSerializable(typeof(ObsWsIdentify))]
    [JsonSerializable(typeof(ObsWsRequest))]
    [JsonSerializable(typeof(ObsWsIdentified))]
    [JsonSerializable(typeof(ObsWsRequestResponse))]
    [JsonSerializable(typeof(ObsWsRequestResponse<ObsWsGetRecordStatusResponse>))]
    [JsonSerializable(typeof(ObsWsRequestResponse<ObsWsGetStreamStatusResponse>))]
    [JsonSerializable(typeof(ObsWsRequestResponseStatus))]
    [JsonSerializable(typeof(ObsWsEvent))]
    public partial class AppJsonSerializerContext : JsonSerializerContext
    {

    }
}

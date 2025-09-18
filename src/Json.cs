using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Watch3.Models;
using Watch3.Models.Cdp;
using Watch3.Models.Obs;

namespace Watch3
{
    [JsonSourceGenerationOptions(
        UseStringEnumConverter = true,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
        NumberHandling = JsonNumberHandling.AllowReadingFromString)]

    [JsonSerializable(typeof(JsonObject))]
    [JsonSerializable(typeof(JsonArray))]
    [JsonSerializable(typeof(List<List<JsonNode>>))]
    [JsonSerializable(typeof(List<Array>))]
    [JsonSerializable(typeof(ErrorResponse))]

    [JsonSerializable(typeof(PushServiceConfig))]
    [JsonSerializable(typeof(AppConfig))]

    [JsonSerializable(typeof(PushSubscription))]
    [JsonSerializable(typeof(RTCSessionDescriptionInit))]
    [JsonSerializable(typeof(SubscriptionInfo))]
    [JsonSerializable(typeof(PushPayload))]
    [JsonSerializable(typeof(EntraIdTokenResponse))]
    [JsonSerializable(typeof(EntraId))]

    [JsonSerializable(typeof(DTCommand))]
    [JsonSerializable(typeof(DTCommandTargetAttachToTarget))]
    [JsonSerializable(typeof(DTCommandPageNavigate))]
    [JsonSerializable(typeof(DTJsonVersionResponse))]
    [JsonSerializable(typeof(DTResponseTargetAttachToTarget))]
    [JsonSerializable(typeof(DTResponseRuntimeCallFunctionOn))]
    [JsonSerializable(typeof(DTResponsePageNavigate))]
    [JsonSerializable(typeof(DTResponseRuntimeExecutionContextCreated))]
    [JsonSerializable(typeof(DTResponsePageLifecycleEvent))]
    [JsonSerializable(typeof(DTResponsePageFrameNavigatedWithinDocument))]
    [JsonSerializable(typeof(DTResponsePageFrameNavigated))]
    [JsonSerializable(typeof(DTResponsePageEnable))]
    [JsonSerializable(typeof(DTResponsePageGetFrameTree))]
    [JsonSerializable(typeof(DTResponseTargetCreateTarget))]
    [JsonSerializable(typeof(DTResponseGetTargets))]
    [JsonSerializable(typeof(DTCommandTargetCreateTarget))]
    [JsonSerializable(typeof(DTCommandTargetActivateTarget))]
    [JsonSerializable(typeof(DTCommandSetCookie))]

    [JsonSerializable(typeof(ObsWsRoot))]
    [JsonSerializable(typeof(ObsWsIdentify))]
    [JsonSerializable(typeof(ObsWsRequest))]
    [JsonSerializable(typeof(ObsWsIdentified))]
    [JsonSerializable(typeof(ObsWsRequestResponse))]
    [JsonSerializable(typeof(ObsWsRequestResponse<ObsWsGetRecordStatusResponse>))]
    [JsonSerializable(typeof(ObsWsRequestResponse<ObsWsGetStreamStatusResponse>))]
    [JsonSerializable(typeof(ObsWsRequestResponseStatus))]
    [JsonSerializable(typeof(ObsWsEvent))]
    public partial class Json : JsonSerializerContext
    {

    }
}

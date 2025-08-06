using System.Text.Json.Nodes;

namespace Watch3.Models.Obs
{
    public sealed record ObsWsRequestResponse(string RequestType, string RequestId, ObsWsRequestResponseStatus RequestStatus);
    public sealed record ObsWsRequestResponse<TData>(string RequestType, string RequestId, ObsWsRequestResponseStatus RequestStatus, TData ResponseData);

    public sealed record ObsWsRequestResponseStatus(bool Result, int Code, string? Comment);
}
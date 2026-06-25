namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpTrafficHistoryResponse
    {
        public IEnumerable<HttpRequestDto> Requests { get; set; } = Array.Empty<HttpRequestDto>();
        public IEnumerable<HttpResponseDto> Responses { get; set; } = Array.Empty<HttpResponseDto>();
        public IEnumerable<HttpBodyDto> RequestBodies { get; set; } = Array.Empty<HttpBodyDto>();
        public IEnumerable<HttpBodyDto> ResponseBodies { get; set; } = Array.Empty<HttpBodyDto>();
    }
}

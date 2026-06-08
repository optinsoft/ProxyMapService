namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpResponseDto
    {
        public required string Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Inbound { get; set; }
        public string? StatusCode { get; set; }
        public string? StatusText { get; set; }
        public string? Route { get; set; }
        public string? TargetHost { get; set; }
        public string? StatusLine { get; set; }
        public Dictionary<string, string> Headers { get; set; } = [];

    }
}

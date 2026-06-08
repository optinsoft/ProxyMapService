namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpRequestDto
    {
        public required string Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Inbound { get; set; }
        public string? RequestURI { get; set; }
        public string? RequestMethod { get; set; }
        public string? Route { get; set; }
        public string? TargetHost { get; set; }
        public string? RequestLine { get; set; }
        public Dictionary<string, string> Headers { get; set; } = [];
    }
}

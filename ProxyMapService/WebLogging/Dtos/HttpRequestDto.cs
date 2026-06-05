namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpRequestDto
    {
        public required string Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Method { get; set; }
        public string? Target { get; set; }
        public string? Route { get; set; }
        public Dictionary<string, string> Headers { get; set; } = [];
    }
}

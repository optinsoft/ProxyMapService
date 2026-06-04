namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpRequestDto
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string? Method { get; set; }
        public string? Url { get; set; }
        public string? Route { get; set; }
        public Dictionary<string, string> Headers { get; set; } = [];
    }
}

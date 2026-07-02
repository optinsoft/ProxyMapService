namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpCompletionDto
    {
        public required string Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}

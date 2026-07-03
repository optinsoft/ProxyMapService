namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpBodyDto : HttpMultipartBodyDto
    {
        public required string Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Completed { get; set; }
    }
}

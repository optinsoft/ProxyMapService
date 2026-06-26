namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpBodyDto
    {
        public required string Id { get; set; }
        public required long Length { get; set; }
        public string? ContentType { get; set; }
        public required HttpBodyContentKind ContentKind { get; set; }
        public string? Content { get; set; }
        public string? BinaryContentBase64 { get; set; }
    }
}

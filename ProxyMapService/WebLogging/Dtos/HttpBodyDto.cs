namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpBodyDto : HttpMultipartBodyDto
    {
        public required string Id { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public bool Completed { get; set; }
        public long? CompressedLength { get; set; }

        public static HttpBodyDto CreateWithoutContent(HttpBodyDto source)
        {
            return new HttpBodyDto
            {
                // HttpMultipartBodyDto
                Length = source.Length,
                ContentType = source.ContentType,
                ContentKind = source.ContentKind,
                HasContent = source.HasContent,
                HasBinaryContent = source.HasBinaryContent,
                Parts = source.Parts,
                // HttpBodyDto
                Id = source.Id,
                Timestamp = source.Timestamp,
                Completed = source.Completed,
                CompressedLength = source.CompressedLength,
            };
        }
    }
}

namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpBodyDto : HttpMultipartBodyDto
    {
        public required string Id { get; set; }
    }
}

namespace ProxyMapService.WebLogging.Dtos
{
    public class HttpMultipartPartDto : HttpMultipartBodyDto
    {
        public string? Name { get; set; }
        public string? FileName { get; set; }
    }
}

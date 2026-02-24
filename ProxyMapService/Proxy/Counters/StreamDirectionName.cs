namespace ProxyMapService.Proxy.Counters
{
    public class StreamDirectionName
    {
        private static readonly string[] _directionNames =
        {
            "downstream client",
            "upstream server"
        };

        public static string GetName(StreamDirection direction) => _directionNames[(int)direction];
    }
}

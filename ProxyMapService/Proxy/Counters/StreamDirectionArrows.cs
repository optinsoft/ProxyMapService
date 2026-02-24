namespace ProxyMapService.Proxy.Counters
{
    public class StreamDirectionArrows
    {
        private static readonly string[] _directionReadArrows =
        {
            ">>>",
            "<<<"
        };

        private static readonly string[] _directionSentArrows =
        {
            "<<<",
            ">>>"
        };

        public static string GetReadArrows(StreamDirection direction) => _directionReadArrows[(int)direction];
        public static string GetSentArrows(StreamDirection direction) => _directionSentArrows[(int)direction];
    }
}

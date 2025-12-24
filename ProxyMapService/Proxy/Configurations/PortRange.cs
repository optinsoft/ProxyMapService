namespace ProxyMapService.Proxy.Configurations
{
    public class PortRange(int start, int end)
    {
        public int Start { get; set; } = start;
        public int End { get; set; } = end;
    }
}

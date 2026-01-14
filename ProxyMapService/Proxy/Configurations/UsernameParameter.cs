namespace ProxyMapService.Proxy.Configurations
{
    public class UsernameParameter
    {
        public string Name { get; init; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public string? Default { get; set; } = null;
        public bool SessionId {  get; set; } = false;
        public bool SessionTime { get; set; } = false;
        public bool Resolved { get; private set; } = false;
        public void SetResolvedValue(string resolvedValue)
        {
            Value = resolvedValue;
            Resolved = true;
        }
    }
}

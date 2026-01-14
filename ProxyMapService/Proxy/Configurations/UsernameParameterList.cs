namespace ProxyMapService.Proxy.Configurations
{
    public class UsernameParameterList
    {
        public List<UsernameParameter> Items { get; init; } = [];

        public UsernameParameter? SessionId => Items.Find(p => p.SessionId);
        public UsernameParameter? SessionTime => Items.Find(p => p.SessionTime);

        private Dictionary<string, UsernameParameter>? _lookup;

        private Dictionary<string, UsernameParameter> Lookup =>
            _lookup ??= Items.ToDictionary(
                p => p.Name,
                StringComparer.OrdinalIgnoreCase);

        public bool Contains(string name)
            => Lookup.ContainsKey(name);

        public UsernameParameter? FindParameter(string name)
            => Lookup.TryGetValue(name, out var param)
                ? param : null;

        public string? GetValue(string name)
            => Lookup.TryGetValue(name, out var param)
                ? param.Value
                : null;

        public void SetValue(string name, string value, UsernameParameter? paramTemplate = null)
        {
            if (Lookup.TryGetValue(name, out var param))
            {
                param.Value = value;
            }
            else
            {
                param = new UsernameParameter
                {
                    Name = name,
                    Value = value,
                    Default = paramTemplate?.Default,
                    SessionId = paramTemplate?.SessionId ?? false,
                    SessionTime = paramTemplate?.SessionTime ?? false
                };
                Items.Add(param);
                Lookup[name] = param;
            }
        }

        public void SetResolvedValue(string name, string value, UsernameParameter? paramTemplate = null)
        {
            if (Lookup.TryGetValue(name, out var param))
            {
                param.SetResolvedValue(value);
            }
            else
            {
                param = new UsernameParameter
                {
                    Name = name,
                    Default = paramTemplate?.Default,
                    SessionId = paramTemplate?.SessionId ?? false,
                    SessionTime = paramTemplate?.SessionTime ?? false
                };
                param.SetResolvedValue(value);
                Items.Add(param);
                Lookup[name] = param;
            }
        }

        public bool Remove(string name)
        {
            if (!Lookup.TryGetValue(name, out var param))
                return false;

            Items.Remove(param);
            Lookup.Remove(name);
            return true;
        }
    }
}

namespace ProxyMapService.Proxy.Configurations
{
    public class UsernameParameterList
    {
        public List<UsernameParameter> Items { get; init; } = [];

        private Dictionary<string, UsernameParameter>? _lookup;

        private Dictionary<string, UsernameParameter> Lookup =>
            _lookup ??= Items.ToDictionary(
                p => p.Name,
                StringComparer.OrdinalIgnoreCase);

        public bool Contains(string name)
            => Lookup.ContainsKey(name);

        public string? GetValue(string name)
            => Lookup.TryGetValue(name, out var param)
                ? param.Value
                : null;

        public void SetValue(string name, string value)
        {
            if (Lookup.TryGetValue(name, out var param))
            {
                param.Value = value;
            }
            else
            {
                var newParam = new UsernameParameter
                {
                    Name = name,
                    Value = value
                };

                Items.Add(newParam);
                Lookup[name] = newParam;
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

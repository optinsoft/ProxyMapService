namespace ProxyMapService.Proxy.Configurations
{
    public class UsernameParameterList : List<UsernameParameter>
    {
        public UsernameParameter? SessionId => this.Find(p => p.SessionId);
        public UsernameParameter? SessionTime => this.Find(p => p.SessionTime);

        private Dictionary<string, UsernameParameter>? _lookup;

        private Dictionary<string, UsernameParameter> Lookup =>
            _lookup ??= this.ToDictionary(
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
                this.Add(param);
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
                this.Add(param);
                Lookup[name] = param;
            }
        }

        public bool Remove(string name)
        {
            if (!Lookup.TryGetValue(name, out var param))
                return false;

            this.Remove(param);
            Lookup.Remove(name);
            return true;
        }
    }
}

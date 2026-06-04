namespace ProxyMapService.Utils
{
    public static class HttpHeaderParser
    {
        public static Dictionary<string, string> ParseRawHeaders(string[]? rawHeaders)
        {
            var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (rawHeaders == null) return dictionary;

            foreach (var headerLine in rawHeaders)
            {
                if (string.IsNullOrWhiteSpace(headerLine)) continue;

                int separatorIndex = headerLine.IndexOf(':');
                if (separatorIndex > 0)
                {
                    string key = headerLine.Substring(0, separatorIndex).Trim();
                    string value = headerLine.Substring(separatorIndex + 1).Trim();

                    if (dictionary.ContainsKey(key))
                        dictionary[key] += $", {value}";
                    else
                        dictionary[key] = value;
                }
            }

            return dictionary;
        }
    }
}

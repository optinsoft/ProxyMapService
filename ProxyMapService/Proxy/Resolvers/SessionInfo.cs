using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Converters;
using System.Text.Json.Serialization;

namespace ProxyMapService.Proxy.Resolvers
{
    public class SessionInfo
    {
        public string? SessionId { get; set; }
        public int? SessionTime { get; set; }

        [JsonConverter(typeof(Rfc1123DateTimeConverter))]
        public DateTime? ExpiresAt { get; set; }
        public Dictionary<string, string>? UsernameParameters { get; set; }
    }
}

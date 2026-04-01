using System.Globalization;

namespace ProxyMapService.Proxy.Cache
{
    public enum CacheDecision
    {
        UseCache,
        ServeStaleWhileRevalidate,
        Revalidate,
        Refresh
    }

    public sealed class CacheEvaluationResult
    {
        public CacheDecision Decision { get; init; }
        public string Reason { get; init; } = string.Empty;
        public TimeSpan? RemainingTtl { get; init; }
        public DateTimeOffset? ExpiresAt { get; init; }
    }

    public static class CachePolicyEvaluator
    {
        private static readonly TimeSpan DefaultHeuristicLifetime = TimeSpan.FromMinutes(10);

        public static CacheEvaluationResult Evaluate(CacheEntry entry, DateTimeOffset now)
        {
            var cc = ParseCacheControl(entry.CacheControl);

            if (cc.ContainsKey("no-store"))
            {
                return Result(CacheDecision.Refresh, "Cache-Control: no-store");
            }

            var requestTime = GetResponseDate(entry);
            var currentAge = now - requestTime;

            if (cc.ContainsKey("no-cache"))
            {
                return HasValidators(entry)
                    ? Result(CacheDecision.Revalidate, "Cache-Control: no-cache + validators")
                    : Result(CacheDecision.Refresh, "Cache-Control: no-cache without validators");
            }

            var freshnessLifetime = GetFreshnessLifetime(entry, cc, requestTime);

            if (freshnessLifetime > TimeSpan.Zero)
            {
                if (currentAge <= freshnessLifetime)
                {
                    return Result(
                        CacheDecision.UseCache,
                        "Fresh by max-age/expires/heuristic",
                        freshnessLifetime - currentAge,
                        requestTime + freshnessLifetime);
                }

                // stale-while-revalidate
                if (cc.TryGetValue("stale-while-revalidate", out var swrValue) &&
                    int.TryParse(swrValue, out var swrSeconds))
                {
                    var staleWindow = freshnessLifetime + TimeSpan.FromSeconds(swrSeconds);
                    if (currentAge <= staleWindow)
                    {
                        return Result(
                            CacheDecision.ServeStaleWhileRevalidate,
                            "Within stale-while-revalidate window",
                            staleWindow - currentAge,
                            requestTime + staleWindow);
                    }
                }
            }

            if (HasValidators(entry))
            {
                return Result(CacheDecision.Revalidate, "Stale, but validators available");
            }

            return Result(CacheDecision.Refresh, "Stale and no validators");
        }

        private static TimeSpan GetFreshnessLifetime(
            CacheEntry entry,
            Dictionary<string, string?> cc,
            DateTimeOffset responseDate)
        {
            // RFC priority #1: max-age
            if (cc.TryGetValue("max-age", out var maxAgeValue) &&
                int.TryParse(maxAgeValue, out var maxAgeSeconds))
            {
                return TimeSpan.FromSeconds(Math.Max(0, maxAgeSeconds));
            }

            // RFC priority #2: Expires - Date
            if (TryParseHttpDate(entry.Expires, out var expires))
            {
                var ttl = expires - responseDate;
                return ttl > TimeSpan.Zero ? ttl : TimeSpan.Zero;
            }

            // RFC heuristic: 10% of (Date - Last-Modified)
            if (TryParseHttpDate(entry.LastModified, out var lastModified) &&
                responseDate > lastModified)
            {
                var resourceAge = responseDate - lastModified;
                var heuristic = TimeSpan.FromTicks(resourceAge.Ticks / 10);
                return heuristic > TimeSpan.Zero ? heuristic : DefaultHeuristicLifetime;
            }

            return DefaultHeuristicLifetime;
        }

        private static bool HasValidators(CacheEntry entry)
        {
            return !string.IsNullOrWhiteSpace(entry.ETag) ||
                   !string.IsNullOrWhiteSpace(entry.LastModified);
        }

        private static DateTimeOffset GetResponseDate(CacheEntry entry)
        {
            if (TryParseHttpDate(entry.Date, out var date))
                return date;

            return new DateTimeOffset(DateTime.SpecifyKind(entry.CreatedAt, DateTimeKind.Utc));
        }

        private static bool TryParseHttpDate(string? value, out DateTimeOffset result)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                result = default;
                return false;
            }

            return DateTimeOffset.TryParseExact(
                value,
                "r",
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out result);
        }

        private static Dictionary<string, string?> ParseCacheControl(string? value)
        {
            var result = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

            if (string.IsNullOrWhiteSpace(value))
                return result;

            foreach (var rawPart in value.Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                var part = rawPart.Trim();
                var kv = part.Split('=', 2);

                if (kv.Length == 1)
                    result[kv[0].Trim()] = null;
                else
                    result[kv[0].Trim()] = kv[1].Trim().Trim('"');
            }

            return result;
        }

        private static CacheEvaluationResult Result(
            CacheDecision decision,
            string reason,
            TimeSpan? ttl = null,
            DateTimeOffset? expiresAt = null)
        {
            return new CacheEvaluationResult
            {
                Decision = decision,
                Reason = reason,
                RemainingTtl = ttl,
                ExpiresAt = expiresAt
            };
        }
    }
}

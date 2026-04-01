using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Headers;
using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;

namespace ProxyMapService.Proxy.Cache
{
    public class CacheManager
    {
        private readonly bool _enabled;
        private readonly string _cacheDir;
        private readonly CacheRepository _repo;
        private readonly ConcurrentDictionary<string, CacheEntry> _memoryIndex;

        public bool Enabled { get => _enabled; }
        public string CacheDir { get => _cacheDir; }
        public CacheRepository Repository { get => _repo; }

        public CacheManager(bool enabled, string cacheDir, CacheRepository repo)
        {
            _enabled = enabled;
            _cacheDir = cacheDir;
            _repo = repo;

            _memoryIndex = new ConcurrentDictionary<string, CacheEntry>();
        }

        public async Task InitAsync()
        {
            if (!_enabled)
                return;

            if (!String.IsNullOrEmpty(_cacheDir))
            {
                Directory.CreateDirectory(_cacheDir);
            }

            await _repo.InitAsync();
        }

        public async Task<CacheEntry?> GetAsync(string host, string url, List<CacheRule> rules)
        {
            if (!_enabled) 
                return null;

            var key = Hash($"{host}{url}");

            var filePath = BuildFilePath(key, createDir: false);
            if (filePath == null)
                return null;

            if (!File.Exists(filePath))
                return null;

            if (_memoryIndex.TryGetValue(key, out var entry))
            {
                var rule = CacheRule.FindCacheContentTypeRule(entry.ContentType, rules);
                if (rule != null)
                {
                    if (!UseCacheEntry(entry, rule))
                        return null;
                    entry.LastAccess = DateTime.UtcNow;
                    await _repo.UpdateAccessAsync(key, entry.LastAccess);
                    return entry;
                }
            }

            var dbEntry = await _repo.GetAsync(key, filePath);
            if (dbEntry != null)
            {
                var rule = CacheRule.FindCacheContentTypeRule(dbEntry.ContentType, rules);
                if (rule == null)
                    return null;
                if (!UseCacheEntry(dbEntry, rule))
                    return null;
                dbEntry.LastAccess = DateTime.UtcNow;
                _memoryIndex[key] = dbEntry;
                await _repo.UpdateAccessAsync(key, dbEntry.LastAccess);
            }

            return dbEntry;
        }

        private static bool UseCacheEntry(CacheEntry entry, CacheRule rule)
        {
            if (rule.MaxAge > 0)
            {
                var expiresAt = entry.CreatedAt.AddSeconds(rule.MaxAge);
                if (expiresAt <= DateTime.UtcNow)
                {
                    return false;
                }
            }

            if (rule.IgnoreCacheControl)
            {
                return true;
            }

            var decision = CachePolicyEvaluator.Evaluate(entry, DateTimeOffset.UtcNow);
            
            var result = (decision.Decision == CacheDecision.UseCache);

            return result;
        }   
  
        public CacheEntry? CreateCacheEntry(string host, string url, HttpResponseHeader responseHeader, CacheRule rule)
        {
            if (!_enabled)
                return null;

            var key = Hash($"{host}{url}");

            var filePath = BuildFilePath(key, createDir: true);
            if (filePath == null)
                return null;

            var entry = new CacheEntry
            {
                Key = key,
                Host = host,
                Url = url,
                ETag = responseHeader.ETag,
                CacheControl = responseHeader.CacheControl,
                Date = responseHeader.Date,
                Expires = responseHeader.Expires,
                LastModified = responseHeader.LastModified,
                FilePath = filePath,
                HeaderLength = responseHeader.HeaderLength,
                ContentLength = responseHeader.ContentLength ?? 0,
                ContentType = responseHeader.ContentType,
                CreatedAt = DateTime.UtcNow,
                LastAccess = DateTime.UtcNow
            };

            if (!UseCacheEntry(entry, rule))
            {
                return null;
            }

            return entry;
        }

        public async Task WriteAsync(CacheEntry entry)
        {
            if (!_enabled) 
                return;

            await _repo.InsertAsync(entry);

            _memoryIndex[entry.Key] = entry;
        }

        private static string Hash(string input)
        {
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
            return Convert.ToHexString(bytes).ToLower();
        }

        private string? BuildFilePath(string key, bool createDir)
        {
            if (String.IsNullOrEmpty(_cacheDir))
                return null;

            if (key.Length < 4) return null;

            var dir1 = Path.Combine(_cacheDir, key.Substring(0, 2));
            if (createDir)
                Directory.CreateDirectory(dir1);

            var dir2 = Path.Combine(dir1, key.Substring(0, 4));
            if (createDir)
                Directory.CreateDirectory(dir2);

            return Path.Combine(dir2, key + ".bin");
        }
    }
}

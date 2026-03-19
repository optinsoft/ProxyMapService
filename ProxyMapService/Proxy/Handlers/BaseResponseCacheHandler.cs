using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Sessions;
using System.Diagnostics;

namespace ProxyMapService.Proxy.Handlers
{
    public class BaseResponseCacheHandler
    {
        protected static async Task<CacheEntry?> GetCacheEntry(SessionContext context)
        {
            if (!context.UseCache)
                return null;

            if (context.RequestHeader?.HTTPVerb != "GET")
                return null;

            var hostname = context.Host.Hostname.ToLower();
            var port = context.Host.Port;

            string? requestUrl = context.RequestHeader?.HTTPTargetPath;
            if (requestUrl == null)
                return null;

            return await context.CacheManager.GetAsync($"{hostname}:{port}", requestUrl);
        }

        protected static async Task<FileStream?> GetCacheFileStream(SessionContext context)
        {
            var cacheEntry = await GetCacheEntry(context);
            if (cacheEntry == null)
                return null;

            return GetCacheEntryFileStream(cacheEntry);
        }

        protected static FileStream? GetCacheEntryFileStream(CacheEntry cacheEntry)
        {
            var fileStream = new FileStream(
                cacheEntry.FilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                64 * 1024,
                useAsync: true);

            if (fileStream.Length != cacheEntry.HeaderLength + cacheEntry.ContentLength)
            {
                using (fileStream)
                {
                }
                fileStream = null;
            }

            return fileStream;
        }

        protected static bool CreateResponseCacheFileStream(SessionContext context)
        {
            if (!context.UseCache)
                return false;

            if (context.RequestHeader?.HTTPVerb != "GET")
                return false;

            var hostname = context.Host.Hostname.ToLower();
            var port = context.Host.Port;

            string? requestUrl = context.RequestHeader.HTTPTargetPath;
            if (requestUrl == null)
                return false;

            if (context.ResponseHeader?.StatusCode != "200")
                return false;

            if (context.ResponseHeader.ContentLength  == null || context.ResponseHeader.ContentLength < 0)
                return false;

            Debug.Assert(context.ResponseCacheEntry == null, "!!! Response cache entry is not null !!!");
            context.ResponseCacheEntry = context.CacheManager.CreateCacheEntry(
                $"{hostname}:{port}", requestUrl, context.ResponseHeader);
            if (context.ResponseCacheEntry == null)
                return false;

            Debug.Assert(context.ResponseCacheFileStream == null, "!!! Cache file stream is not null !!!");
            context.CreateResponseCacheFileStream();

            return true;
        }

        protected static async Task HandleEndOfResponseCacheFileStream(SessionContext context)
        {
            if (context.ResponseCacheFileStream != null)
            {
                if (context.ResponseCacheEntry != null)
                {
                    var diff = context.ResponseCacheFileStream.Length - (context.ResponseCacheEntry.HeaderLength + context.ResponseCacheEntry.ContentLength);
                    if (diff >= 0)
                    {
                        Debug.Assert(diff == 0, "!!! Wrong cache file size !!!");
                        context.DisposeResponseCacheFileStream();
                        if (diff == 0)
                        {
                            await context.CacheManager.WriteAsync(context.ResponseCacheEntry);
                        }
                    }
                }
            }
        }
    }
}

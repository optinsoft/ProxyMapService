namespace ProxyMapService.WebLogging
{
    public static class MimeTypeProvider
    {
        private static readonly Dictionary<string, string> MimeToTypeMap = new(StringComparer.OrdinalIgnoreCase)
        {
            // Documents
            { "text/html", "document" },
            { "application/xhtml+xml", "document" },
        
            // Scripts
            { "text/javascript", "script" },
            { "application/javascript", "script" },
            { "application/x-javascript", "script" },
            { "application/x-ecmascript", "script" },
            { "text/ecmascript", "script" },
        
            // Stylesheets
            { "text/css", "stylesheet" },
        
            // Images
            { "image/jpeg", "image" },
            { "image/png", "image" },
            { "image/gif", "image" },
            { "image/webp", "image" },
            { "image/svg+xml", "image" },
            { "image/x-icon", "image" },
            { "image/bmp", "image" },
            { "image/avif", "image" },
        
            // Fonts
            { "font/woff", "font" },
            { "font/woff2", "font" },
            { "font/ttf", "font" },
            { "font/otf", "font" },
            { "application/font-woff", "font" },
            { "application/x-font-woff", "font" },
            { "application/x-font-ttf", "font" },
        
            // JSON / XML
            { "application/json", "json" },
            { "text/json", "json" },
            { "application/xml", "xml" },
            { "text/xml", "xml" },
        
            // Events / Streams
            { "text/event-stream", "eventsource" }
        };

        /// <summary>
        /// Returns the Type category based on the Content-Type header, similar to Chrome DevTools.
        /// </summary>
        /// <param name="contentTypeHeader">The header value (e.g., "text/html; charset=UTF-8")</param>
        /// <returns>A string representation of the type (document, script, image, media, etc.)</returns>
        public static string GetNetworkType(string contentTypeHeader)
        {
            if (string.IsNullOrWhiteSpace(contentTypeHeader))
            {
                return "other";
            }

            string mimeType = contentTypeHeader.Split(';')[0].Trim().ToLowerInvariant();

            if (MimeToTypeMap.TryGetValue(mimeType, out var mappedType))
            {
                return mappedType;
            }

            if (mimeType.StartsWith("image/")) return "image";
            if (mimeType.StartsWith("audio/") || mimeType.StartsWith("video/") || mimeType == "application/ogg") return "media";
            if (mimeType.StartsWith("font/")) return "font";

            if (mimeType.Contains("manifest")) return "manifest";
            if (mimeType.StartsWith("text/")) return "document";

            return "other";
        }
    }
}

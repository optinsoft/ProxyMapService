namespace ProxyMapService.Proxy.Configurations
{
    /// <summary>
    /// Resolves TlsClientType.Auto from the client (browser) User-Agent:
    /// Firefox → Nss, Chrome-based browsers → BoringSsl, anything else → Native.
    /// </summary>
    public static class TlsClientTypeResolver
    {
        public static TlsClientType Resolve(string? userAgent)
        {
            if (string.IsNullOrEmpty(userAgent))
            {
                return TlsClientType.Native;
            }

            // Firefox first: Firefox User-Agents never contain "Chrome/".
            if (userAgent.Contains("Firefox/", StringComparison.OrdinalIgnoreCase))
            {
                return TlsClientType.Nss;
            }

            // Chromium-based browsers: Chrome, Edge ("Edg/"), Opera ("OPR/"), Chromium etc.
            // all report "Chrome/" in the User-Agent.
            if (userAgent.Contains("Chrome/", StringComparison.OrdinalIgnoreCase))
            {
                return TlsClientType.BoringSsl;
            }

            return TlsClientType.Native;
        }
    }
}

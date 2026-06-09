namespace ProxyMapService.Proxy.Network
{
    public static class NetworkSecurityHelper
    {
        // Using HashSet ensures maximum lookup speed with O(1) time complexity
        private static readonly HashSet<int> StandardTlsPorts = new HashSet<int>
        {
            443,  // HTTPS (Web traffic)
            465,  // SMTPS (Secure email sending)
            563,  // NNTPS (Secure news)
            636,  // LDAPS (Secure directory access)
            990,  // FTPS (Secure file transfer control)
            992,  // Telnet over TLS
            993,  // IMAPS (Secure email retrieval)
            995,  // POP3S (Secure email retrieval)
            3269, // Microsoft Global Catalog over SSL
            8443  // Alternative HTTPS / Management
        };

        /// <summary>
        /// Checks if the specified port is a standard port configured for TLS encryption.
        /// </summary>
        /// <param name="port">The TCP port number to validate.</param>
        /// <returns>True if the port is a standard TLS port; otherwise, false.</returns>
        public static bool IsStandardTlsPort(int port)
        {
            // Validate the TCP port range
            if (port < 1 || port > 65535)
            {
                return false;
            }

            return StandardTlsPorts.Contains(port);
        }
    }
}

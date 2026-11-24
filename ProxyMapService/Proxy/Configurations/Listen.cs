namespace ProxyMapService.Proxy.Configurations
{
    public class Listen
    {
        private PortRange _portRange = new();

        public int Port {
            get
            {
                return _portRange.Start;
            }
            set
            {
                _portRange.Start = value;
                _portRange.End = value;
            }
        }

        public PortRange PortRange {
            get
            {
                return _portRange;
            }
            set
            {
                _portRange = value;
            }
        }

        /// <summary>
        /// Network address to bind the listener(s) to. Default: "Loopback" (127.0.0.1).
        /// Supported values: "Loopback"/"Localhost", a concrete IP address (e.g. "192.168.1.10",
        /// "::1"), "Any" or "*" for all interfaces (IPv6 dual-mode, accepts IPv4 and IPv6),
        /// "Any6" for all IPv6 interfaces only, "Any4" for all IPv4 interfaces only.
        /// </summary>
        public string? IPAddress { get; set; }
        public bool RejectHttpProxy { get; set; } = false;
        public int StickyProxyLifetime { get; set; } = 0;
        /// <summary>
        /// Forward (default) — the classic forward proxy; the target host is named by the client.
        /// Reverse — a reverse proxy; the target host is taken from the ReverseProxy settings.
        /// </summary>
        public ModeEnum Mode { get; set; } = ModeEnum.Forward;
        public ActionEnum Action { get; set; } = ActionEnum.Allow;
        public bool DecryptSSL { get; set; } = false;
        /// <summary>
        /// Optional splitting of the first outgoing TLS record (ClientHello) into several
        /// separate TLS records (like TlsRecordSplit --split). Comma-separated split positions:
        /// syntax "offset[:repeats:skip][+flag1[flag2]]", e.g. "1", "3+s", "0+sm", "-5".
        /// Flags: +s — SNI start, +e — end, +m — middle, +n — zero (absolute) offset.
        /// Null/empty — disabled. Applied whenever the outgoing connection is TLS, both in
        /// MITM mode (DecryptSSL, the proxy generates the ClientHello) and in passthrough mode
        /// (the client's own ClientHello is re-chunked on the fly).
        /// </summary>
        public string? TlsRecordSplit { get; set; }
        public bool IgnoreCertificateErrors { get; set; } = false;
        public SslMode SslMode { get; set; } = SslMode.Auto;
        public SslMode UpstreamSslMode { get; set; } = SslMode.Auto;
        public bool? EnableTranslateH1ToH2 { get; set; }
        public bool? DisableDowngradeToHttp1ForCache { get; set; }
        public bool? EnableDowngradeToHttp1ForOverwrittenHost { get; set; }
        public bool IgnoreHostRules { get; set; } = false;
        /// <summary>
        /// Rules for editing request headers sent to the upstream server, merged over the global
        /// HTTP:RequestHeaders (see HeadersRule).
        /// </summary>
        public HeadersRule? RequestHeaders { get; set; }
        /// <summary>
        /// Rules for editing response headers sent back to the client, merged over the global
        /// HTTP:ResponseHeaders (see HeadersRule).
        /// </summary>
        public HeadersRule? ResponseHeaders { get; set; }
        /// <summary>
        /// Partial override of the global SslClientOptions for this listening port range.
        /// </summary>
        public SslClientOptionsOverride? SslClientOptions { get; set; }
        /// <summary>
        /// Partial override of the global SslServerOptions for this listening port range.
        /// </summary>
        public SslServerOptionsOverride? SslServerOptions { get; set; }
    }
}

// Ignore Spelling: Proxified

using ProxyMapService.Proxy.Authenticator;
using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Providers;
using ProxyMapService.Proxy.Resolvers;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Sessions
{
    public class SessionContext : IDisposable, IHttpLoggersProvider
    {
        private readonly HostAddress _host;
        private HttpRequestHeader? _requestHeader;
        private HttpResponseHeader? _responseHeader;
        private List<CacheRule> _requestCacheRules;

        public TcpClient IncomingClient { get; private set; }
        public TcpClient OutgoingClient { get; private set; }
        public ProxyMapping Mapping { get; private set; }
        public bool Ssl { get; set; }
        public bool UpstreamSsl { get; set; }
        public SslClientOptionsConfig SslClientConfig { get; private set; }
        public SslServerOptionsConfig SslServerConfig { get; private set; }
        public X509Certificate2? ServerCertificate { get; set; }
        public X509Certificate2? CACertificate { get; set; }
        public IProxyProvider ProxyProvider { get; private set; }
        public IProxyAuthenticator ProxyAuthenticator { get; private set; }
        public IUsernameParameterResolver UsernameParameterResolver { get; private set; }
        public List<HostRule> HostRules { get; private set; }
        public List<CacheRule> CacheRules { get; set; }
        public CacheManager CacheManager { get; private set; }
        public string? UserAgent { get; private set; }
        public ProxyCounters ProxyCounters { get; private set; }
        public ILogger Logger { get; private set; }
        public CancellationToken Token { get; private set; }

        public ReadHeaderStream IncomingHeaderStream { get; private set; }
        public ReadHeaderStream OutgoingHeaderStream { get; private set; }

        public CountingStream? IncomingStream { get; set; }
        public CountingStream? OutgoingStream { get; set; }

        public System.Net.EndPoint? IncomingEndPoint { get; set; }
        public System.Net.IPEndPoint? OutgoingEndPoint {  get; set; }

        public HttpRequestHeader? Http { get; set; }
        public Socks4Header? Socks4 { get; set; }
        public Socks5Header? Socks5 { get; set; }
        public HostAddress Host { 
            get => _host;
            set
            {
                _host.Assign(value);
            }
        }
        public ActionEnum? HostAction { get; set; }
        public ProxyServer? ProxyServer { get; set; }
        public bool Proxified { get; set; }
        public bool Bypassed { get; set; }
        public bool FileRequested { get; set; }
        public UsernameParameterList? UsernameParameters { get; set; }
        public string? SessionId { get; set; }
        public int SessionTime { get; set; }
        public string? RootDir { get; set; }
        public TunnelState RequestTunnelState { get; private set; }
        public TunnelState ResponseTunnelState { get; private set; }
        public HttpRequestHeader? RequestHeader
        {
            get => _requestHeader;
            set
            {
                _requestHeader = value;
                if (_requestHeader != null)
                {
                    _requestCacheRules = CacheRule.FindRules(_requestHeader.HTTPTargetPath, _requestHeader.Accept, CacheRules);
                }
                else
                {
                    _requestCacheRules = [];
                }
            }
        }
        public HttpResponseHeader? ResponseHeader
        {
            get => _responseHeader;
            set
            {
                _responseHeader = value;
            }
        }
        public CacheEntry? ResponseCacheEntry { get; set; }
        public FileStream? ResponseCacheFileStream { get; set; }
        public List<CacheRule> RequestCacheRules { get => _requestCacheRules; }
        public bool CachedReply { get; set; }

        IHttpHeadersLogger IHttpLoggersProvider.RequestHeadersLogger { get => ProxyCounters.HttpRequestHeadersLogger; }
        IHttpHeadersLogger IHttpLoggersProvider.ResponseHeadersLogger { get => ProxyCounters.HttpResponseHeadersLogger; }
        string IHttpLoggersProvider.GetRoute()
        {
            return (this switch
            {
                { Http: not null } => "http",
                { Socks4: not null } => "socks4",
                { Socks5: not null } => "socks5",
                _ => ""
            });
        }

        public SessionContext(TcpClient incomingClient, System.Net.EndPoint? incomingEndPoint, 
            ProxyMapping mapping, bool ssl, 
            X509Certificate2? serverCertificate, X509Certificate2? caCertificate,
            IProxyProvider proxyProvider, IProxyAuthenticator proxyAuthenticator,
            IUsernameParameterResolver usernameParameterResolver, List<HostRule> hostRules, 
            List<CacheRule> cacheRules, CacheManager cacheManager, string? userAgent,
            SslClientOptionsConfig sslClientConfig, SslServerOptionsConfig sslServerConfig,
            ProxyCounters proxyCounters, ILogger logger, CancellationToken token)
        {
            IncomingClient = incomingClient;
            IncomingEndPoint = incomingEndPoint;
            OutgoingClient = new TcpClient();
            OutgoingEndPoint = null;
            Mapping = mapping;
            Ssl = ssl;
            UpstreamSsl = ssl;
            ServerCertificate = serverCertificate;
            CACertificate = caCertificate;
            ProxyProvider = proxyProvider;
            ProxyAuthenticator = proxyAuthenticator;
            UsernameParameterResolver = usernameParameterResolver;
            HostRules = hostRules;
            CacheRules = cacheRules;
            CacheManager = cacheManager;
            UserAgent = userAgent;
            SslClientConfig = sslClientConfig;
            SslServerConfig = sslServerConfig;
            ProxyCounters = proxyCounters;
            Logger = logger;
            Token = token;
            IncomingHeaderStream = new ReadHeaderStream();
            OutgoingHeaderStream = new ReadHeaderStream();
            _host = new HostAddress("", 0);
            RequestTunnelState = new TunnelState
            {
                Response = false
            };
            ResponseTunnelState = new TunnelState
            {
                Response = true
            };
            _requestCacheRules = [];
        }

        public void CreateIncomingClientStream()
        {
            IncomingStream = new CountingStream(IncomingClient.GetStream(), this, 
                ProxyCounters.IncomingReadCounter, ProxyCounters.IncomingSendCounter,
                RequestTunnelState.TunnelId, ResponseTunnelState.TunnelId);
        }

        public void CreateOutgoingClientStream()
        {
            OutgoingStream = new CountingStream(OutgoingClient.GetStream(), this, 
                ProxyCounters.OutgoingReadCounter, ProxyCounters.OutgoingSendCounter,
                ResponseTunnelState.TunnelId, RequestTunnelState.TunnelId);
        }

        public void CreateResponseCacheFileStream()
        {
            if (ResponseCacheEntry != null)
            {
                DisposeResponseCacheFileStream();
                ResponseCacheFileStream = new FileStream(
                    ResponseCacheEntry.FilePath,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    64 * 1024,
                    useAsync: true);
            }
        }

        public void DisposeIncomingClientStream()
        {
            if (IncomingStream != null)
            {
                using (IncomingStream)
                {
                }
                IncomingStream = null;
            }
        }

        public void DisposeOutgoingClientStream()
        {
            if (OutgoingStream != null)
            {
                using (OutgoingStream)
                {
                }
                OutgoingStream = null;
            }
        }

        public void DisposeResponseCacheFileStream()
        {
            if (ResponseCacheFileStream != null)
            {
                using (ResponseCacheFileStream)
                {
                }
                ResponseCacheFileStream = null;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                DisposeIncomingClientStream();
                DisposeOutgoingClientStream();
                OutgoingClient.Dispose();
                IncomingHeaderStream.Dispose();
                OutgoingHeaderStream.Dispose();
                DisposeResponseCacheFileStream();
            }
        }
    }
}
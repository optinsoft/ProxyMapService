// Ignore Spelling: Proxified

using ProxyMapService.Proxy.Authenticator;
using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Http;
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
        private IBodyTracker? _responseBodyTracker;
        private List<CacheRule> _requestCacheRules;
        private string? _requestId;

        public System.Net.IPEndPoint InboundEndpoint {  get; private set; }
        public TcpClient IncomingClient { get; private set; }
        public TcpClient OutgoingClient { get; private set; }
        public ProxyMapping Mapping { get; private set; }
        public SessionAPIConfig SessionAPI { get; set; }
        public bool DecryptSSL { get;set; }
        public SslMode SslMode { get; set; }
        public SslMode UpstreamSslMode { get; set; }
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

        public ProxyType? InboundType { get; set; }

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
        public SessionAction? HostAction { get; set; }
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
        public IBodyTracker? ResponseBodyTracker
        {
            get => _responseBodyTracker;
            set
            {
                _responseBodyTracker = value;
            }
        }
        public CacheEntry? ResponseCacheEntry { get; set; }
        public FileStream? ResponseCacheFileStream { get; set; }
        public List<CacheRule> RequestCacheRules { get => _requestCacheRules; }
        public bool CachedReply { get; set; }

        IHttpHeadersLogger? IHttpLoggersProvider.RequestHeadersLogger { get => ProxyCounters.HttpRequestHeadersLogger; }
        IHttpHeadersLogger? IHttpLoggersProvider.ResponseHeadersLogger { get => ProxyCounters.HttpResponseHeadersLogger; }
        IHttpBodyLogger? IHttpLoggersProvider.RequestBodyLogger { get => ProxyCounters.HttpRequestBodyLogger; }
        IHttpBodyLogger? IHttpLoggersProvider.ResponseBodyLogger { get => ProxyCounters.HttpResponseBodyLogger; }
        string IHttpLoggersProvider.GetRequestId()
        {
            _requestId = Guid.NewGuid().ToString();
            return _requestId;
        }
        string IHttpLoggersProvider.GetResponseId()
        {
            return _requestId ?? Guid.NewGuid().ToString();
        }
        string? IHttpLoggersProvider.GetInbound()
        {
            return (InboundType switch
            {
                ProxyType.Http => $"http://{InboundEndpoint}",
                ProxyType.Socks4 => $"socks4://{InboundEndpoint}",
                ProxyType.Socks5 => $"socks5://{InboundEndpoint}",
                _ => null
            });
        }
        string? IHttpLoggersProvider.GetRoute()
        {
            string cachePrefix = CachedReply ? "(cache) " : "";
            switch (ProxyServer?.ProxyType)
            {
                case ProxyType.Http:
                    return $"{cachePrefix}http://{ProxyServer.Host}:{ProxyServer.Port}";
                case ProxyType.Socks4:
                    return $"{cachePrefix}socks4://{ProxyServer.Host}:{ProxyServer.Port}";
                case ProxyType.Socks5:
                    return $"{cachePrefix}socks5://{ProxyServer.Host}:{ProxyServer.Port}";
                default:
                    break;
            }
            if (HostAction == null) return null;
            switch (HostAction.Value.ActionValue)
            {
                case SessionActionEnum.Allow:
                    return $"{cachePrefix}proxy";
                case SessionActionEnum.Bypass:
                    return $"{cachePrefix}direct";
                case SessionActionEnum.File:
                    return $"{cachePrefix}file";
                case SessionActionEnum.SessionAPI:
                    return $"{cachePrefix}session api";
                default:
                    //SessionActionEnum.Deny
                    return "deny";
            }
        }
        string? IHttpLoggersProvider.GetTargetHost()
        {
            if (Host.Hostname.Length == 0) return null;
            return $"{Host.Hostname}:{Host.Port}";
        }

        public SessionContext(System.Net.IPEndPoint inboundEndpoint, TcpClient incomingClient, 
            System.Net.EndPoint? incomingEndPoint, ProxyMapping mapping, SessionAPIConfig sessionAPI, 
            X509Certificate2? serverCertificate, X509Certificate2? caCertificate,
            IProxyProvider proxyProvider, IProxyAuthenticator proxyAuthenticator,
            IUsernameParameterResolver usernameParameterResolver, List<HostRule> hostRules, 
            List<CacheRule> cacheRules, CacheManager cacheManager, string? userAgent,
            SslClientOptionsConfig sslClientConfig, SslServerOptionsConfig sslServerConfig,
            ProxyCounters proxyCounters, ILogger logger, CancellationToken token)
        {
            InboundEndpoint = inboundEndpoint;
            IncomingClient = incomingClient;
            IncomingEndPoint = incomingEndPoint;
            OutgoingClient = new TcpClient();
            OutgoingEndPoint = null;
            Mapping = mapping;
            SessionAPI = sessionAPI;
            DecryptSSL = mapping.Listen.DecryptSSL;
            SslMode = mapping.Listen.SslMode;
            UpstreamSslMode = mapping.Listen.UpstreamSslMode;
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
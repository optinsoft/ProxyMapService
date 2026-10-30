// Ignore Spelling: Proxified

using ProxyMapService.Proxy.Authenticator;
using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Providers;
using ProxyMapService.Proxy.RoutingLoopDetector;
using ProxyMapService.Proxy.Tls;
using System.Collections.Concurrent;
using System.Net.Security;
using System.Net.Sockets;
using System.Security.Cryptography.X509Certificates;
using HttpRequestHeader = ProxyMapService.Proxy.Headers.HttpRequestHeader;
using HttpResponseHeader = ProxyMapService.Proxy.Headers.HttpResponseHeader;

namespace ProxyMapService.Proxy.Sessions
{
    public class SessionContext : IDisposable, IHttpLoggersProvider
    {
        private readonly HostAddress _host;
        private HttpRequestHeader? _requestHeader;
        private HttpResponseHeader? _responseHeader;
        private List<CacheRule> _requestCacheRules;
        private int _lastRequestNum = 0;
        private int _lastResponseNum = 0;

        public System.Net.IPEndPoint InboundEndpoint {  get; private set; }
        public TcpClient IncomingClient { get; private set; }
        public TcpClient OutgoingClient { get; private set; }
        public ProxyMapping Mapping { get; private set; }
        public IRoutingLoopDetector RoutingLoopDetector { get; private set; }
        public SessionAPIConfig SessionAPI { get; set; }
        public SessionAction Action { get; set; }
        public bool DecryptSSL { get; set; }
        public bool IgnoreCertificateErrors {  get; set; }
        public SslMode SslMode { get; set; }
        public SslMode UpstreamSslMode { get; set; }
        public bool H1ToH2TranslationEnabled { get; set; }
        public bool DowngradeToHttp1ForCache { get; set; }
        public bool IgnoreHostRules { get; set; }
        public SslClientOptionsConfig SslClientConfig { get; private set; }
        public SslServerOptionsConfig SslServerConfig { get; private set; }
        public X509Certificate2? ServerCertificate { get; set; }
        public X509Certificate2? CACertificate { get; set; }
        public IProxyProvider ProxyProvider { get; private set; }
        public IProxyAuthenticator ProxyAuthenticator { get; private set; }
        public ISessionManager SessionManager { get; private set; }
        public List<HostRule> HostRules { get; private set; }
        public List<CacheRule> CacheRules { get; set; }
        public CacheManager CacheManager { get; private set; }
        public string? UserAgent { get; private set; }
        public ProxyCounters ProxyCounters { get; private set; }
        public ILogger Logger { get; private set; }
        public CancellationToken Token { get; private set; }
        public CancellationToken TunnelToken { get; set; }

        public ReadHeaderStream IncomingHeaderStream { get; private set; }
        public ReadHeaderStream OutgoingHeaderStream { get; private set; }

        public CountingStream? IncomingStream { get; set; }
        public CountingStream? OutgoingStream { get; set; }

        public System.Net.EndPoint? IncomingEndPoint { get; set; }
        public System.Net.IPEndPoint? OutgoingEndPoint {  get; set; }

        public bool IncomingHttp2 { get; set; }
        public bool OutgoingHttp2 { get; set; }

        public ClientHelloInfo? ClientHello {  get; set; }
        public SslApplicationProtocol NegotiatedAlpn { get; set; } = new();
        public bool TranslateH1ToH2 { get; set; }
        public bool DowngradeClientToHttp1 { get; set; }
        public bool RequiresHttp1Client => Host.Overwritten || (DowngradeToHttp1ForCache && CacheRules is { Count: > 0 });
        public Http2Connection? OutgoingHttp2Connection { get; set; }
        public Http2TrafficObserver? Http2Observer { get; set; }

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
                if (_requestHeader == value) return;
                _requestHeader = value;
                if (_requestHeader != null)
                {
                    _requestHeader.RequestNum = Interlocked.Increment(ref _lastRequestNum);
                    RequestHeaderHistory.TryAdd(_requestHeader.RequestNum, _requestHeader);
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
                if (_responseHeader == value) return;
                _responseHeader = value;
                if (_responseHeader != null)
                {
                    _responseHeader.ResponseNum = Interlocked.Increment(ref _lastResponseNum);
                }
            }
        }
        public IHttpBodyTracker? RequestBodyTracker { get; set; }
        public IHttpBodyTracker? ResponseBodyTracker { get; set; }
        public CacheEntry? ResponseCacheEntry { get; set; }
        public FileStream? ResponseCacheFileStream { get; set; }
        public List<CacheRule> RequestCacheRules { get => _requestCacheRules; }
        public bool CachedReply { get; set; }

        public ConcurrentDictionary<int, HttpRequestHeader> RequestHeaderHistory { get; private set; } = new();

        // IHttpLoggersProvider
        public IHttpHeadersLogger? RequestHeadersLogger { get => ProxyCounters.HttpRequestHeadersLogger; }
        public IHttpHeadersLogger? ResponseHeadersLogger { get => ProxyCounters.HttpResponseHeadersLogger; }
        public IHttpCompletionLogger? CompletionLogger { get => ProxyCounters.HttpCompletionLogger; }
        public IHttpBodyLogger? RequestBodyLogger { get => ProxyCounters.HttpRequestBodyLogger; }
        public IHttpBodyLogger? ResponseBodyLogger { get => ProxyCounters.HttpResponseBodyLogger; }
        public string? GetInbound()
        {
            return (InboundType switch
            {
                ProxyType.Http => $"http://{InboundEndpoint}",
                ProxyType.Socks4 => $"socks4://{InboundEndpoint}",
                ProxyType.Socks5 => $"socks5://{InboundEndpoint}",
                _ => null
            });
        }
        public string? GetRoute()
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
                case SessionActionEnum.Proxy:
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
        public string? GetTargetHost()
        {
            if (Host.Hostname.Length == 0) return null;
            return $"{Host.Hostname}:{Host.Port}";
        }

        public SessionContext(System.Net.IPEndPoint inboundEndpoint, TcpClient incomingClient, 
            System.Net.EndPoint? incomingEndPoint, ProxyMapping mapping, 
            IRoutingLoopDetector loopDetector, SessionAPIConfig sessionAPI, 
            X509Certificate2? serverCertificate, X509Certificate2? caCertificate,
            IProxyProvider proxyProvider, IProxyAuthenticator proxyAuthenticator,
            ISessionManager sessionManager, List<HostRule> hostRules, 
            List<CacheRule> cacheRules, CacheManager cacheManager, HttpConfig httpConfig,
            SslClientOptionsConfig sslClientConfig, SslServerOptionsConfig sslServerConfig,
            ProxyCounters proxyCounters, ILogger logger, CancellationToken token)
        {
            InboundEndpoint = inboundEndpoint;
            IncomingClient = incomingClient;
            IncomingEndPoint = incomingEndPoint;
            OutgoingClient = new TcpClient();
            OutgoingEndPoint = null;
            Mapping = mapping;
            RoutingLoopDetector = loopDetector;
            SessionAPI = sessionAPI;
            Action = mapping.Listen.Action switch {
                ActionEnum.Allow => SessionActionEnum.Proxy,
                ActionEnum.Deny => SessionActionEnum.Deny,
                ActionEnum.Bypass => SessionActionEnum.Bypass,
                ActionEnum.File => SessionActionEnum.File,
                ActionEnum.Proxy => SessionActionEnum.Proxy,
                _ => SessionActionEnum.Proxy
            };
            DecryptSSL = mapping.Listen.DecryptSSL;
            IgnoreCertificateErrors = mapping.Listen.IgnoreCertificateErrors;
            SslMode = mapping.Listen.SslMode;
            UpstreamSslMode = mapping.Listen.UpstreamSslMode;
            H1ToH2TranslationEnabled = mapping.Listen.EnableTranslateH1ToH2 ?? httpConfig.EnableTranslateH1ToH2;
            DowngradeToHttp1ForCache = mapping.Listen.EnableDowngradeToHttp1ForCache ?? httpConfig.EnableDowngradeToHttp1ForCache;
            IgnoreHostRules = mapping.Listen.IgnoreHostRules;
            ServerCertificate = serverCertificate;
            CACertificate = caCertificate;
            ProxyProvider = proxyProvider;
            ProxyAuthenticator = proxyAuthenticator;
            SessionManager = sessionManager;
            HostRules = hostRules;
            CacheRules = cacheRules;
            CacheManager = cacheManager;
            UserAgent = httpConfig.UserAgent;
            SslClientConfig = sslClientConfig;
            SslServerConfig = sslServerConfig;
            ProxyCounters = proxyCounters;
            Logger = logger;
            Token = token;
            IncomingHeaderStream = new ReadHeaderStream();
            OutgoingHeaderStream = new ReadHeaderStream();
            _host = new HostAddress("", 0);
            RequestTunnelState = new TunnelState();
            ResponseTunnelState = new TunnelState();
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

        public void DisposeRequestBodyTracker()
        {
            if (RequestBodyTracker != null)
            {
                using (RequestBodyTracker)
                {
                }
                RequestBodyTracker = null;
            }
        }

        public void DisposeResponseBodyTracker()
        {
            if (ResponseBodyTracker != null)
            {
                using (ResponseBodyTracker)
                {
                }
                ResponseBodyTracker = null;
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
                DisposeRequestBodyTracker();
                DisposeResponseBodyTracker();
                Http2Observer?.Close();
            }
        }

        public void CompleteRequest(HttpRequestHeader? requestHeader)
        {
            if (requestHeader == null || !requestHeader.TryMarkCompleted()) return;
            RequestHeaderHistory.TryRemove(requestHeader.RequestNum, out _);
            CompletionLogger?.OnHttpCompleted(this, requestHeader);
        }

        public void CompleteAllRequests()
        {
            // finish everything that didn't finish on its own: aborted requests, "until connection close" responses
            foreach (var request in RequestHeaderHistory.OrderBy(kv => kv.Key).Select(kv => kv.Value))
            {
                CompleteRequest(request);
            }
            CompleteRequest(RequestHeader);
        }
    }
}
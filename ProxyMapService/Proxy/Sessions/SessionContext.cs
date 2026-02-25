// Ignore Spelling: Proxified

using ProxyMapService.Proxy.Authenticator;
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
    public class SessionContext : IDisposable
    {
        private HostAddress _host;

        public TcpClient IncomingClient { get; private set; }
        public TcpClient OutgoingClient { get; private set; }
        public ProxyMapping Mapping { get; private set; }
        public bool Ssl { get; set; }
        public bool UpstreamSsl { get; set; }
        public SslClientOptionsConfig SslClientConfig { get; private set; }
        public SslServerOptionsConfig SslServerConfig { get; private set; }
        public X509Certificate2? ServerCertificate { get; set; }
        public IProxyProvider ProxyProvider { get; private set; }
        public IProxyAuthenticator ProxyAuthenticator { get; private set; }
        public IUsernameParameterResolver UsernameParameterResolver { get; private set; }
        public List<HostRule> HostRules { get; private set; }
        public string? UserAgent { get; private set; }
        public ISessionsCounter? SessionsCounter { get; private set; }
        public IBytesReadCounter? OutgoingReadCounter { get; private set; }
        public IBytesSentCounter? OutgoingSentCounter { get; private set; }
        public IBytesReadCounter? IncomingReadCounter { get; private set; }
        public IBytesSentCounter? IncomingSentCounter { get; private set; }
        public IBytesReadCounter? IncomingSslCounter { get; private set; }
        public IBytesReadCounter? OutgoingSslCounter { get; private set; }
        public ILogger Logger { get; private set; }
        public CancellationToken Token { get; private set; }

        public ReadHeaderStream IncomingHeaderStream { get; private set; }
        public ReadHeaderStream OutgoingHeaderStream { get; private set; }

        public CountingStream? IncomingStream { get; set; }
        public CountingStream? OutgoingStream { get; set; }
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
        public string? FilesDir { get; set; }

        public SessionContext(TcpClient incomingClient, ProxyMapping mapping, 
            bool ssl, X509Certificate2? serverCertificate,
            IProxyProvider proxyProvider, IProxyAuthenticator proxyAuthenticator,
            IUsernameParameterResolver usernameParameterResolver,
            List<HostRule> hostRules, string? userAgent,
            SslClientOptionsConfig sslClientConfig, SslServerOptionsConfig sslServerConfig,
            ISessionsCounter? sessionsCounter, 
            IBytesReadCounter? outgoingReadCounter, IBytesSentCounter? outgoingSentCounter,
            IBytesReadCounter? incomingReadCounter, IBytesSentCounter? incomingSentCounter,
            IBytesReadCounter? incomingSslCounter, IBytesReadCounter? outgoingSslCounter,
            ILogger logger, CancellationToken token)
        {
            IncomingClient = incomingClient;
            OutgoingClient = new TcpClient();
            Mapping = mapping;
            Ssl = ssl;
            UpstreamSsl = ssl;
            ServerCertificate = serverCertificate;
            ProxyProvider = proxyProvider;
            ProxyAuthenticator = proxyAuthenticator;
            UsernameParameterResolver = usernameParameterResolver;
            HostRules = hostRules;
            UserAgent = userAgent;
            SslClientConfig = sslClientConfig;
            SslServerConfig = sslServerConfig;
            SessionsCounter = sessionsCounter;
            OutgoingReadCounter = outgoingReadCounter;
            OutgoingSentCounter = outgoingSentCounter;
            IncomingReadCounter = incomingReadCounter;
            IncomingSentCounter = incomingSentCounter;
            IncomingSslCounter = incomingSslCounter;
            OutgoingSslCounter = outgoingSslCounter;
            Logger = logger;
            Token = token;
            IncomingHeaderStream = new ReadHeaderStream(this, incomingReadCounter);
            OutgoingHeaderStream = new ReadHeaderStream(this, outgoingReadCounter);
            _host = new HostAddress("", 0);
        }

        public void CreateIncomingClientStream()
        {
            IncomingStream = new CountingStream(IncomingClient.GetStream(), this, IncomingReadCounter, IncomingSentCounter);
        }

        public void CreateOutgoingClientStream()
        {
            OutgoingStream = new CountingStream(OutgoingClient.GetStream(), this, OutgoingReadCounter, OutgoingSentCounter);
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
                if (IncomingStream != null)
                {
                    using (IncomingStream)
                    {
                    }
                    IncomingStream = null;
                }
                if (OutgoingStream != null)
                {
                    using (OutgoingStream)
                    {
                    }
                    OutgoingStream = null;
                }
                OutgoingClient.Dispose();
                IncomingHeaderStream.Dispose();
                OutgoingHeaderStream.Dispose();
            }
        }
    }
}
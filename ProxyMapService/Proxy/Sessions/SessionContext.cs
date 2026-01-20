// Ignore Spelling: Proxified

using ProxyMapService.Proxy.Authenticator;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Providers;
using ProxyMapService.Proxy.Resolvers;
using System.Collections.Specialized;
using System.Net.Sockets;

namespace ProxyMapService.Proxy.Sessions
{
    public class SessionContext : IDisposable
    {
        public TcpClient IncomingClient { get; private set; }
        public TcpClient OutgoingClient { get; private set; }
        public ProxyMapping Mapping { get; private set; }
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
        public ILogger Logger { get; private set; }
        public CancellationToken Token { get; private set; }

        public ReadHeaderStream IncomingHeaderStream { get; private set; }
        public ReadHeaderStream OutgoingHeaderStream { get; private set; }

        public CountingStream? IncomingStream { get; set; }
        public CountingStream? OutgoingStream { get; set; }
        public HttpRequestHeader? Http { get; set; }
        public Socks4Header? Socks4 { get; set; }
        public Socks5Header? Socks5 { get; set; }
        public string HostName { get; set; }
        public int HostPort { get; set; }
        public ActionEnum? HostAction { get; set; }
        public ProxyServer? ProxyServer { get; set; }
        public bool Proxified { get; set; }
        public bool Bypassed { get; set; }
        public bool FileRequested { get; set; }
        public UsernameParameterList? UsernameParameters { get; set; }
        public string? SessionId { get; set; }
        public int SessionTime { get; set; }
        public string? FilesDir { get; set; }

        public SessionContext(TcpClient incomingClient, ProxyMapping mapping, IProxyProvider proxyProvider, 
            IProxyAuthenticator proxyAuthenticator, IUsernameParameterResolver usernameParameterResolver,
            List<HostRule> hostRules, string? userAgent, ISessionsCounter? sessionsCounter, 
            IBytesReadCounter? outgoingReadCounter, IBytesSentCounter? outgoingSentCounter,
            IBytesReadCounter? incomingReadCounter, IBytesSentCounter? incomingSentCounter,
            ILogger logger, CancellationToken token)
        {
            IncomingClient = incomingClient;
            OutgoingClient = new TcpClient();
            Mapping = mapping;
            ProxyProvider = proxyProvider;
            ProxyAuthenticator = proxyAuthenticator;
            UsernameParameterResolver = usernameParameterResolver;
            HostRules = hostRules;
            UserAgent = userAgent;
            SessionsCounter = sessionsCounter;
            OutgoingReadCounter = outgoingReadCounter;
            OutgoingSentCounter = outgoingSentCounter;
            IncomingReadCounter = incomingReadCounter;
            IncomingSentCounter = incomingSentCounter;
            Logger = logger;
            Token = token;
            IncomingHeaderStream = new ReadHeaderStream(this, incomingReadCounter);
            OutgoingHeaderStream = new ReadHeaderStream(this, outgoingReadCounter);
            HostName = "";
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
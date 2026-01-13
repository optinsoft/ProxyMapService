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
        public TcpClient Client { get; private set; }
        public TcpClient RemoteClient { get; private set; }
        public ProxyMapping Mapping { get; private set; }
        public IProxyProvider ProxyProvider { get; private set; }
        public IProxyAuthenticator ProxyAuthenticator { get; private set; }
        public IUsernameParameterResolver UsernameParameterResolver { get; private set; }
        public List<HostRule> HostRules { get; private set; }
        public string? UserAgent { get; private set; }
        public ISessionsCounter? SessionsCounter { get; private set; }
        public IBytesReadCounter? RemoteReadCounter { get; private set; }
        public IBytesSentCounter? RemoteSentCounter { get; private set; }
        public IBytesReadCounter? ClientReadCounter { get; private set; }
        public IBytesSentCounter? ClientSentCounter { get; private set; }
        public ILogger Logger { get; private set; }
        public CancellationToken Token { get; private set; }

        public ReadHeaderStream ClientHeaderStream { get; private set; }
        public ReadHeaderStream RemoteHeaderStream { get; private set; }

        public CountingStream? ClientStream { get; set; }
        public CountingStream? RemoteStream { get; set; }
        public HttpRequestHeader? Http { get; set; }
        public Socks4Header? Socks4 { get; set; }
        public Socks5Header? Socks5 { get; set; }
        public string HostName { get; set; }
        public int HostPort { get; set; }
        public ActionEnum? HostAction { get; set; }
        public ProxyServer? ProxyServer { get; set; }
        public bool Proxified { get; set; }
        public bool Bypassed { get; set; }
        public UsernameParameterList? UsernameParameters { get; set; }

        public SessionContext(TcpClient client, ProxyMapping mapping, IProxyProvider proxyProvider, 
            IProxyAuthenticator proxyAuthenticator, IUsernameParameterResolver usernameParameterResolver,
            List<HostRule> hostRules, string? userAgent, ISessionsCounter? sessionsCounter, 
            IBytesReadCounter? remoteReadCounter, IBytesSentCounter? remoteSentCounter,
            IBytesReadCounter? clientReadCounter, IBytesSentCounter? clientSentCounter,
            ILogger logger, CancellationToken token)
        {
            Client = client;
            RemoteClient = new TcpClient();
            Mapping = mapping;
            ProxyProvider = proxyProvider;
            ProxyAuthenticator = proxyAuthenticator;
            UsernameParameterResolver = usernameParameterResolver;
            HostRules = hostRules;
            UserAgent = userAgent;
            SessionsCounter = sessionsCounter;
            RemoteReadCounter = remoteReadCounter;
            RemoteSentCounter = remoteSentCounter;
            ClientReadCounter = clientReadCounter;
            ClientSentCounter = clientSentCounter;
            Logger = logger;
            Token = token;
            ClientHeaderStream = new ReadHeaderStream(this, clientReadCounter);
            RemoteHeaderStream = new ReadHeaderStream(this, remoteReadCounter);
            HostName = "";
        }

        public void CreateClientStream()
        {
            ClientStream = new CountingStream(Client.GetStream(), this, ClientReadCounter, ClientSentCounter);
        }

        public void CreateRemoteClientStream()
        {
            RemoteStream = new CountingStream(RemoteClient.GetStream(), this, RemoteReadCounter, RemoteSentCounter);
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
                if (ClientStream != null)
                {
                    using (ClientStream)
                    {
                    }
                    ClientStream = null;
                }
                if (RemoteStream != null)
                {
                    using (RemoteStream)
                    {
                    }
                    RemoteStream = null;
                }
                RemoteClient.Dispose();
                ClientHeaderStream.Dispose();
                RemoteHeaderStream.Dispose();
            }
        }
    }
}
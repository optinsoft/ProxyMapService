using System.Net.Sockets;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Headers;

namespace ProxyMapService.Proxy.Sessions
{
    public class SessionContext : IDisposable
    {
        public TcpClient Client { get; private set; }
        public TcpClient RemoteClient { get; private set; }
        public ProxyMapping Mapping { get; private set; }
        public List<HostRule>? HostRules { get; private set; }
        public string? UserAgent { get; private set; }
        public SessionsCounter? SessionsCounter { get; private set; }
        public BytesReadCounter? ReadCounter { get; private set; }
        public BytesSentCounter? SentCounter { get; private set; }
        public ILogger Logger { get; private set; }
        public CancellationToken Token { get; private set; }

        public ReadHeaderStream ClientHeaderStream { get; private set; }
        public ReadHeaderStream RemoteHeaderStream { get; private set; }

        public NetworkStream? ClientStream { get; set; }
        public NetworkStream? RemoteStream { get; set; }
        public byte[]? HeaderBytes { get; set; }
        public HttpRequestHeader? Http { get; set; }
        public Socks4Header? Socks4 { get; set; }
        public Socks5Header? Socks5 { get; set; }
        public string HostName { get; set; }
        public int HostPort { get; set; }
        public ActionEnum? HostAction { get; set; }
        public bool Proxified { get; set; }
        public bool Bypassed { get; set; }

        public SessionContext(TcpClient client, ProxyMapping mapping, List<HostRule>? hostRules, string? userAgent,
            SessionsCounter? sessionsCounter, BytesReadCounter? readCounter, BytesSentCounter? sentCounter,
            ILogger logger, CancellationToken token)
        {
            Client = client;
            RemoteClient = new TcpClient();
            Mapping = mapping;
            HostRules = hostRules;
            UserAgent = userAgent;
            SessionsCounter = sessionsCounter;
            ReadCounter = readCounter;
            SentCounter = sentCounter;
            Logger = logger;
            Token = token;
            ClientHeaderStream = new ReadHeaderStream(this, null);
            RemoteHeaderStream = new ReadHeaderStream(this, readCounter);
            HostName = "";
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
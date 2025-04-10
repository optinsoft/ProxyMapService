using System.Net.Sockets;
using System.Net;
using ProxyMapService.Proxy.Configurations;
using Proxy.Headers;
using ProxyMapService.Proxy.Counters;

namespace ProxyMapService.Proxy.Sessions
{
    public class SessionContext(TcpClient client, ProxyMapping mapping, List<HostRule>? hostRules,
        SessionsCounter? sessionsCounter, BytesReadCounter? readCounter, BytesSentCounter? sentCounter, 
        ILogger logger, CancellationToken token) : IDisposable
    {
        public TcpClient Client { get; private set; } = client;
        public TcpClient RemoteClient { get; set; } = new TcpClient();
        public ProxyMapping Mapping { get; private set; } = mapping;
        public List<HostRule>? HostRules { get; private set; } = hostRules;
        public SessionsCounter? SessionsCounter { get; private set; } = sessionsCounter;
        public BytesReadCounter? ReadCounter { get; private set; } = readCounter;
        public BytesSentCounter? SentCounter { get; private set; } = sentCounter;
        public ILogger Logger { get; private set; } = logger;
        public CancellationToken Token { get; private set; } = token;

        public NetworkStream? ClientStream { get; set; }
        public HttpHeader? Header { get; set; }
        public string HostName { get; set; } = "";
        public int HostPort { get; set; }
        public ActionEnum? HostAction { get; set; }
        public bool Proxified { get; set; }
        public bool Bypassed { get; set; }
        public byte[]? TunnelHeaderBytes { get; set; }

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
                RemoteClient.Dispose();
            }
        }
    }
}
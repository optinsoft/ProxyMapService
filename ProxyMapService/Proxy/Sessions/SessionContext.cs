using System.Net.Sockets;
using System.Net;
using ProxyMapService.Proxy.Configurations;
using Proxy.Headers;

namespace ProxyMapService.Proxy.Sessions
{
    public class SessionContext(TcpClient client, ProxyMapping mapping, ILogger logger, CancellationToken token) : IDisposable
    {
        public TcpClient Client { get; private set; } = client;
        public ProxyMapping mapping { get; private set; } = mapping;
        public ILogger Logger { get; private set; } = logger;
        public CancellationToken Token { get; private set; } = token;

        public NetworkStream? ClientStream { get; set; }
        public HttpHeader? Header { get; set; }

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
            }
        }
    }
}
using Proxy.Network;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public class SessionsCounter
    {
        private readonly object _lock = new();
        public int Count { get; private set; }
        public int AuthenticationNotRequired { get; private set; }
        public int AuthenticationRequired { get; private set; }
        public int Authenticated { get; private set; }
        public int AuthenticationInvalid { get; private set; }
        public int HttpRejected { get; private set; }
        public int Connected { get; private set; }
        public int ConnectionFailed { get; private set; }
        public int HeaderFailed { get; private set; }
        public int HostFailed { get; private set; }
        public int HTTPRequests { get; private set; }

        public void OnSessionStarted(SessionContext context)
        {
            lock (_lock)
            {
                Count += 1;
            }
            SessionStartedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnAuthenticationNotRequired(SessionContext context)
        {
            lock (_lock)
            {
                AuthenticationNotRequired += 1;
            }
            AuthenticationNotRequiredHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnAuthenticationRequired(SessionContext context)
        {
            lock (_lock)
            {
                AuthenticationRequired += 1;
            }
            AuthenticationRequiredHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnAuthenticated(SessionContext context)
        {
            lock (_lock)
            {
                Authenticated += 1;
            }
            AuthenticatedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnAuthenticationInvalid(SessionContext context)
        {
            lock (_lock)
            {
                AuthenticationInvalid += 1;
            }
            AuthenticationInvalidHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnHttpRejected(SessionContext context)
        {
            lock (_lock)
            {
                HttpRejected += 1;
            }
            HttpRejectedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnConnected(SessionContext context)
        {
            lock (_lock)
            {
                Connected += 1;
            }
            ConnectedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnConnectionFailed(SessionContext context)
        {
            lock (_lock)
            {
                ConnectionFailed += 1;
            }
            ConnectionFailedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnHeaderFailed(SessionContext context)
        {
            lock (_lock)
            {
                HeaderFailed += 1;
            }
            HeaderFailedHander?.Invoke(context, EventArgs.Empty);
        }

        public void OnHostFailed(SessionContext context)
        {
            lock (_lock)
            {
                HostFailed += 1;
            }
            HostFailedHander?.Invoke(context, EventArgs.Empty);
        }

        public void OnHTTPRequest(SessionContext context)
        {
            lock (_lock)
            {
                HTTPRequests += 1;
            }
            HTTPRequestHandler?.Invoke(context, EventArgs.Empty);
        }

        public event EventHandler? SessionStartedHandler;
        public event EventHandler? AuthenticationNotRequiredHandler;
        public event EventHandler? AuthenticationRequiredHandler;
        public event EventHandler? AuthenticatedHandler;
        public event EventHandler? AuthenticationInvalidHandler;
        public event EventHandler? HttpRejectedHandler;
        public event EventHandler? ConnectedHandler;
        public event EventHandler? ConnectionFailedHandler;
        public event EventHandler? HeaderFailedHander;
        public event EventHandler? HostFailedHander;
        public event EventHandler? HTTPRequestHandler;
    }
}

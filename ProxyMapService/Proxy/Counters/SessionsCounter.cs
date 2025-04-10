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
        public int ProxyConnected { get; private set; }
        public int ProxyFailed { get; private set; }
        public int BypassConnected { get; private set; }
        public int BypassFailed { get; private set; }
        public int HeaderFailed { get; private set; }
        public int NoHost { get; private set; }
        public int HostRejected { get; private set; }
        public int HostProxified { get; private set; }
        public int HostBypassed { get; private set; }

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

        public void OnProxyConnected(SessionContext context)
        {
            lock (_lock)
            {
                ProxyConnected += 1;
            }
            ProxyConnectedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnProxyFailed(SessionContext context)
        {
            lock (_lock)
            {
                ProxyFailed += 1;
            }
            ProxyFailedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnBypassConnected(SessionContext context)
        {
            lock (_lock)
            {
                BypassConnected += 1;
            }
            BypassConnectedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnBypassFailed(SessionContext context)
        {
            lock (_lock)
            {
                BypassFailed += 1;
            }
            BypassFailedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnHeaderFailed(SessionContext context)
        {
            lock (_lock)
            {
                HeaderFailed += 1;
            }
            HeaderFailedHander?.Invoke(context, EventArgs.Empty);
        }

        public void OnNoHost(SessionContext context)
        {
            lock (_lock)
            {
                NoHost += 1;
            }
            NoHostHander?.Invoke(context, EventArgs.Empty);
        }

        public void OnHostRejected(SessionContext context)
        {
            lock (_lock)
            {
                HostRejected += 1;
            }
            HostRejectedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnHostProxified(SessionContext context)
        {
            lock (_lock)
            {
                HostProxified += 1;
            }
            HostProxifiedHandler?.Invoke(context, EventArgs.Empty);
        }

        public void OnHostBypassed(SessionContext context)
        {
            lock (_lock)
            {
                HostBypassed += 1;
            }
            HostBypassedHandler?.Invoke(context, EventArgs.Empty);
        }

        public event EventHandler? SessionStartedHandler;
        public event EventHandler? AuthenticationNotRequiredHandler;
        public event EventHandler? AuthenticationRequiredHandler;
        public event EventHandler? AuthenticatedHandler;
        public event EventHandler? AuthenticationInvalidHandler;
        public event EventHandler? HttpRejectedHandler;
        public event EventHandler? ProxyConnectedHandler;
        public event EventHandler? ProxyFailedHandler;
        public event EventHandler? BypassConnectedHandler;
        public event EventHandler? BypassFailedHandler;
        public event EventHandler? HeaderFailedHander;
        public event EventHandler? NoHostHander;
        public event EventHandler? HostRejectedHandler;
        public event EventHandler? HostProxifiedHandler;
        public event EventHandler? HostBypassedHandler;
    }
}

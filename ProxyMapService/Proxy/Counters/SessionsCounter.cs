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

        public void OnSessionStarted()
        {
            lock (_lock)
            {
                Count += 1;
            }
            SessionStartedHandler?.Invoke(this, EventArgs.Empty);
        }

        public void OnAuthenticationNotRequired()
        {
            lock (_lock)
            {
                AuthenticationNotRequired += 1;
            }
            AuthenticationNotRequiredHandler?.Invoke(this, EventArgs.Empty);
        }

        public void OnAuthenticationRequired()
        {
            lock (_lock)
            {
                AuthenticationRequired += 1;
            }
            AuthenticationRequiredHandler?.Invoke(this, EventArgs.Empty);
        }

        public void OnAuthenticated()
        {
            lock (_lock)
            {
                Authenticated += 1;
            }
            AuthenticatedHandler?.Invoke(this, EventArgs.Empty);
        }

        public void OnAuthenticationInvalid()
        {
            lock (_lock)
            {
                AuthenticationInvalid += 1;
            }
            AuthenticationInvalidHandler?.Invoke(this, EventArgs.Empty);
        }

        public void OnHttpRejected()
        {
            lock (_lock)
            {
                HttpRejected += 1;
            }
            HttpRejectedHandler?.Invoke(this, EventArgs.Empty);
        }

        public void OnConnected()
        {
            lock (_lock)
            {
                Connected += 1;
            }
            ConnectedHandler?.Invoke(this, EventArgs.Empty);
        }

        public void OnConnectionFailed()
        {
            lock (_lock)
            {
                ConnectionFailed += 1;
            }
            ConnectionFailedHandler?.Invoke(this, EventArgs.Empty);
        }

        public void OnHeaderFailed()
        {
            lock (_lock)
            {
                HeaderFailed += 1;
            }
            HeaderFailedHander?.Invoke(this, EventArgs.Empty);
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
    }
}

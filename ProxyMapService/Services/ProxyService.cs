using ProxyMapService.Counters;
using ProxyMapService.Exceptions;
using ProxyMapService.Interfaces;
using ProxyMapService.Models;
using ProxyMapService.Proxy;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Utils;

namespace ProxyMapService.Services
{
    public class ProxyService : IProxyService
    {
        private readonly string _serviceInfo = $"Service created at {DateTime.Now}";
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly SessionsCounter _sessionsCounter = new();
        private readonly HostsCounter _hostsCounter = new();
        private readonly BytesReadCounter _remoteReadCounter = new("remote");
        private readonly BytesSentCounter _remoteSentCounter = new("remote");
        private readonly BytesReadCounter _clientReadCounter = new("client");
        private readonly BytesSentCounter _clientSentCounter = new("client");
        private readonly BytesLogger? _bytesLogger = null;
        private const int _maxListenerStartRetries = 10;
        private CancellationToken _stoppingToken = CancellationToken.None;
        private CancellationTokenSource _cts = new();
        private bool _started = false;
        private DateTime? _startTime = null;
        private DateTime? _stopTime = null;
        private string _serviceId = RandomStringGenerator.GenerateRandomString(6);
        
        public CancellationToken StoppingToken { 
            get => _stoppingToken; 
            set => _stoppingToken = value;
        }
        
        public bool Started
        {
            get => _started;
        }

        public ProxyService(IConfiguration configuration, ILogger<ProxyService> logger)
        {
            _configuration = configuration;
            _logger = logger;
            var HostStatsEnabled = _configuration.GetSection("HostStats")?.GetValue<bool>("Enabled") ?? false;
            if (HostStatsEnabled)
            {
                _sessionsCounter.HostProxifiedHandler += _hostsCounter.OnHostConnected;
                _sessionsCounter.HostBypassedHandler += _hostsCounter.OnHostConnected;
                var HostTrafficStatsEnabled = _configuration.GetSection("HostStats")?.GetValue<bool>("TrafficStats") ?? false;
                if (HostTrafficStatsEnabled)
                {
                    _remoteReadCounter.BytesReadHandler += _hostsCounter.OnBytesRead;
                    _remoteSentCounter.BytesSentHandler += _hostsCounter.OnBytesSent;
                }
                var LogTrafficData = _configuration.GetSection("HostStats")?.GetValue<bool>("LogTrafficData") ?? false;
                if (LogTrafficData)
                {
                    _bytesLogger = new BytesLogger(_logger);
                    _remoteReadCounter.BytesReadHandler += _bytesLogger.LogBytesRead;
                    _remoteSentCounter.BytesSentHandler += _bytesLogger.LogBytesSent;
                    _clientReadCounter.BytesReadHandler += _bytesLogger.LogBytesRead;
                    _clientSentCounter.BytesSentHandler += _bytesLogger.LogBytesSent;
                }
            }
            _logger.LogInformation(
                "[ProxyService.{}] Service is created at {}.",
                _serviceId,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        }

        public void ResetStats()
        {
            _sessionsCounter.Reset();
            _hostsCounter.Reset();
            _remoteReadCounter.Reset();
            _remoteSentCounter.Reset();
        }

        public void StartProxyMappingTasks()
        {
            if (_started)
            {
                throw new ServiceAlreadyStartedException();
            }

            var hostRules = _configuration.GetSection("HostRules").Get<List<HostRule>>();
            var proxyMappings = _configuration.GetSection("ProxyMappings").Get<List<ProxyMapping>>();
            var userAgent = _configuration.GetSection("HTTP").GetValue<string>("UserAgent");
            
            if (proxyMappings == null || proxyMappings.Count == 0)
            {
                throw new NoMappingsException();
            }

            _cts = new CancellationTokenSource();
            CancellationToken cancellationToken = _stoppingToken == CancellationToken.None
                ? _cts.Token
                : CancellationTokenSource.CreateLinkedTokenSource(_stoppingToken, _cts.Token).Token;
            string serviceId = _serviceId;
            _logger.LogInformation(
                "[ProxyService.{}] Starting proxy mapping tasks at {}...",
                serviceId,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
            List<Task> tasks = [];
            foreach (var mapping in proxyMappings)
            {
                tasks.Add(new ProxyMapper().Start(mapping, hostRules, userAgent,
                    _sessionsCounter, _remoteReadCounter, _remoteSentCounter, 
                    _clientReadCounter, _clientSentCounter, _logger, 
                    _maxListenerStartRetries, cancellationToken));
            }
            _started = true;
            _startTime = DateTime.Now;
            Task allTasks = Task.WhenAll(tasks);
#pragma warning disable 4014
            allTasks.ContinueWith(
                t =>
                {
                    _started = false;
                    _stopTime = DateTime.Now;
                    _logger.LogInformation(
                        "[ProxyService.{}] Proxy mapping tasks have been terminated at {}. Cancellation {} requested.",
                        serviceId,
                        DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"),
                        cancellationToken.IsCancellationRequested ? "has been" : "is not");
                }, 
                TaskContinuationOptions.None);
#pragma warning restore 4014
            _logger.LogInformation(
                "[ProxyService.{}] Proxy mapping tasks have been started at {}.",
                _serviceId,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        }

        public void StopProxyMappingTasks()
        {
            if (!_started)
            {
                throw new ServiceAlreadyTerminatedException();
            }
            _logger.LogInformation("Terminating proxy mapping tasks...");
            _cts.Cancel();
        }

        public string GetServiceInfo()
        {
            return _serviceInfo;
        }

        public string? GetStartTime()
        {
            if (_startTime == null) return null;
            return $"{_startTime}";
        }

        public string? GetStopTime()
        {
            if (_stopTime == null) return null;
            return $"{_stopTime}";
        }

        public string GetCurrentTime()
        {
            return $"{DateTime.Now}";
        }

        public int GetSessionsCount()
        {
            return _sessionsCounter.Count;
        }

        public int GetAuthenticationNotRequired()
        {
            return _sessionsCounter.AuthenticationNotRequired;
        }
        
        public int GetAuthenticationRequired()
        {
            return _sessionsCounter.AuthenticationRequired;
        }
        
        public int GetAuthenticated()
        {
            return _sessionsCounter.Authenticated;
        }
        
        public int GetAuthenticationInvalid()
        {
            return _sessionsCounter.AuthenticationInvalid;
        }

        public int GetHttpRejected()
        {
            return _sessionsCounter.HttpRejected;
        }

        public int GetProxyConnected()
        {
            return _sessionsCounter.ProxyConnected;
        }
        
        public int GetProxyFailed()
        {
            return _sessionsCounter.ProxyFailed;
        }

        public int GetBypassConnected()
        {
            return _sessionsCounter.BypassConnected;
        }

        public int GetBypassFailed()
        {
            return _sessionsCounter.BypassFailed;
        }

        public int GetHeaderFailed()
        {
            return _sessionsCounter.HeaderFailed;
        }

        public int GetNoHost()
        {
            return _sessionsCounter.NoHost;
        }

        public int GetHostRejected()
        {
            return _sessionsCounter.HostRejected;
        }

        public int GetHostProxified()
        {
            return _sessionsCounter.HostProxified;
        }

        public int GetHostBypassed()
        {
            return _sessionsCounter.HostBypassed;
        }

        public long GetTotalBytesRead()
        {
            return _remoteReadCounter.TotalBytesRead;
        }

        public long GetTotalBytesSent()
        {
            return _remoteSentCounter.TotalBytesSent;
        }

        public long GetProxyBytesRead()
        {
            return _remoteReadCounter.ProxyBytesRead;
        }

        public long GetProxyBytesSent()
        {
            return _remoteSentCounter.ProxyBytesSent;
        }

        public long GetBypassBytesRead()
        {
            return _remoteReadCounter.BypassBytesRead;
        }

        public long GetBypassBytesSent()
        {
            return _remoteSentCounter.BypassBytesSent;
        }

        public IEnumerable<KeyValuePair<string, HostStats>>? GetHostStats()
        {
            if (_hostsCounter == null)
            {
                return [];
            }
            return _hostsCounter.GetHostStats();
        }
    }
}

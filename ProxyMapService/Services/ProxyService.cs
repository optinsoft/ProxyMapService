// Ignore Spelling: Proxified

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
        private readonly BytesReadCounter _outgoingReadCounter = new("outgoing client");
        private readonly BytesSentCounter _outgoingSentCounter = new("outgoing client");
        private readonly BytesReadCounter _incomingReadCounter = new("incoming client");
        private readonly BytesSentCounter _incomingSentCounter = new("incoming client");
        private readonly BytesLogger? _bytesLogger = null;
        private const int _maxListenerStartRetries = 10;
        private CancellationToken _stoppingToken = CancellationToken.None;
        private CancellationTokenSource _cts = new();
        private bool _started = false;
        private DateTime? _startTime = null;
        private DateTime? _stopTime = null;
        private readonly string _serviceId = RandomStringGenerator.GenerateRandomString(6);
        private readonly List<HostRule> _hostRules = [];
        private readonly List<IConfigurationRoot> _hostRuleFileConfigurations = [];
        
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
                    _outgoingReadCounter.BytesReadHandler += _hostsCounter.OnBytesRead;
                    _outgoingSentCounter.BytesSentHandler += _hostsCounter.OnBytesSent;
                }
                var LogTrafficData = _configuration.GetSection("HostStats")?.GetValue<bool>("LogTrafficData") ?? false;
                if (LogTrafficData)
                {
                    _bytesLogger = new BytesLogger(_logger);
                    _outgoingReadCounter.BytesReadHandler += _bytesLogger.LogBytesRead;
                    _outgoingSentCounter.BytesSentHandler += _bytesLogger.LogBytesSent;
                    _incomingReadCounter.BytesReadHandler += _bytesLogger.LogBytesRead;
                    _incomingSentCounter.BytesSentHandler += _bytesLogger.LogBytesSent;
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
            _outgoingReadCounter.Reset();
            _outgoingSentCounter.Reset();
        }

        private void LoadHostRules()
        {
            _hostRules.Clear();
            var hostRules = _configuration.GetSection("HostRules").Get<HostRules>();
            if (hostRules != null) _hostRules.AddRange(hostRules.Items);
            _hostRuleFileConfigurations.Clear();
            if (hostRules != null)
            {
                foreach (var hostRulesFile in hostRules.Files)
                {
                    var fileConfig = new ConfigurationBuilder()
                        .SetBasePath(Directory.GetCurrentDirectory())
                        .AddJsonFile(hostRulesFile.Path, optional: false)
                        .Build();
                    _hostRuleFileConfigurations.Add(fileConfig);
                }
            }
            foreach (var fileConfig in _hostRuleFileConfigurations)
            {
                var addHostRules = fileConfig.GetSection("Items").Get<List<HostRule>>();
                if (addHostRules != null)
                {
                    _hostRules.AddRange(addHostRules);
                }
            }
        }

        public void StartProxyMappingTasks()
        {
            if (_started)
            {
                throw new ServiceAlreadyStartedException();
            }

            LoadHostRules();

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
                tasks.Add(new ProxyMapper(mapping, _hostRules, userAgent,
                    _sessionsCounter, _outgoingReadCounter, _outgoingSentCounter, 
                    _incomingReadCounter, _incomingSentCounter, _logger, 
                    _maxListenerStartRetries, cancellationToken).Start());
            }
            _started = true;
            _startTime = DateTime.Now;
            Task allTasks = Task.WhenAll(tasks);
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
            return _outgoingReadCounter.TotalBytesRead;
        }

        public long GetTotalBytesSent()
        {
            return _outgoingSentCounter.TotalBytesSent;
        }

        public long GetProxyBytesRead()
        {
            return _outgoingReadCounter.ProxyBytesRead;
        }

        public long GetProxyBytesSent()
        {
            return _outgoingSentCounter.ProxyBytesSent;
        }

        public long GetBypassBytesRead()
        {
            return _outgoingReadCounter.BypassBytesRead;
        }

        public long GetBypassBytesSent()
        {
            return _outgoingSentCounter.BypassBytesSent;
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

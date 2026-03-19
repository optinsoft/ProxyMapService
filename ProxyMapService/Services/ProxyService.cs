// Ignore Spelling: Proxified

using ProxyMapService.Counters;
using ProxyMapService.Exceptions;
using ProxyMapService.Interfaces;
using ProxyMapService.Models;
using ProxyMapService.Proxy;
using ProxyMapService.Proxy.Cache;
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
        private readonly ProxyCounters _proxyCounters = new();
        private readonly HostsCounter _hostsCounter = new();
        private readonly BytesLogger? _bytesLogger = null;
        private const int _maxListenerStartRetries = 10;
        private CancellationToken _stoppingToken = CancellationToken.None;
        private CancellationTokenSource _cts = new();
        private bool _started = false;
        private DateTime? _startTime = null;
        private DateTime? _stopTime = null;
        private readonly string _serviceId = RandomStringGenerator.GenerateRandomString(6);
        private readonly List<HostRule> _hostRules = [];
        private readonly List<CacheRule> _cacheRules = [];
        private CacheRepository? _cacheRepository;
        private CacheManager? _cacheManager;
        private SslClientOptionsConfig _sslClientOptions = new();
        private SslServerOptionsConfig _sslServerOptions = new();

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
            _bytesLogger = new BytesLogger(_logger);
            var HostStatsEnabled = _configuration.GetSection("HostStats")?.GetValue<bool>("Enabled") ?? false;
            if (HostStatsEnabled)
            {
                _proxyCounters.SessionsCounter.HostProxifiedHandler += _hostsCounter.OnHostConnected;
                _proxyCounters.SessionsCounter.HostBypassedHandler += _hostsCounter.OnHostConnected;
                var HostTrafficStatsEnabled = _configuration.GetSection("HostStats")?.GetValue<bool>("TrafficStats") ?? false;
                if (HostTrafficStatsEnabled)
                {
                    _proxyCounters.OutgoingReadCounter.BytesReadHandler += _hostsCounter.OnBytesRead;
                    _proxyCounters.OutgoingSentCounter.BytesSentHandler += _hostsCounter.OnBytesSent;
                }
            }
            var LogReading = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogReading") ?? false;
            if (LogReading)
            {
                _proxyCounters.IncomingReadCounter.LogReading = true;
                _proxyCounters.OutgoingReadCounter.LogReading = true;
            }
            var LogSending = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogSending") ?? false;
            if (LogSending)
            {
                _proxyCounters.IncomingSentCounter.LogSending = true;
                _proxyCounters.OutgoingSentCounter.LogSending = true;
            }
            var LogTrafficData = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogTrafficData") ?? false;
            if (LogTrafficData)
            {
                _proxyCounters.OutgoingReadCounter.BytesReadHandler += _bytesLogger.LogBytesRead;
                _proxyCounters.OutgoingSentCounter.BytesSentHandler += _bytesLogger.LogBytesSent;
                _proxyCounters.IncomingReadCounter.BytesReadHandler += _bytesLogger.LogBytesRead;
                _proxyCounters.IncomingSentCounter.BytesSentHandler += _bytesLogger.LogBytesSent;
            }
            var LogSslDecodedData = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogSslDecodedData") ?? false;
            if (LogSslDecodedData)
            {
                _proxyCounters.IncomingReadSslCounter.BytesReadHandler += _bytesLogger.LogSslBytesDecoded;
                _proxyCounters.OutgoingReadSslCounter.BytesReadHandler += _bytesLogger.LogSslBytesDecoded;
            }
            var LogSslEncodedData = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogSslEncodedData") ?? false;
            if (LogSslEncodedData)
            {
                _proxyCounters.IncomingSentSslCounter.BytesSentHandler += _bytesLogger.LogSslBytesEncoded;
                _proxyCounters.OutgoingSentSslCounter.BytesSentHandler += _bytesLogger.LogSslBytesEncoded;
            }
            var LogHttpRequestHeaders = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogHttpRequestHeaders") ?? false;
            if (LogHttpRequestHeaders)
            {
                _proxyCounters.HttpRequestHeadersLogger.HttpHeadersHandler += _bytesLogger.LogHttpHeaders;
            }
            var LogHttpResponseHeaders = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogHttpResponseHeaders") ?? false;
            if (LogHttpResponseHeaders)
            {
                _proxyCounters.HttpResponseHeadersLogger.HttpHeadersHandler += _bytesLogger.LogHttpHeaders;
            }
            _logger.LogInformation(
                "[ProxyService.{}] Service is created at {}.",
                _serviceId,
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff"));
        }

        public void ResetStats()
        {
            _proxyCounters.ResetStats();
            _hostsCounter.Reset();
        }

        public void StartProxyMappingTasks()
        {
            if (_started)
            {
                throw new ServiceAlreadyStartedException();
            }

            HostRules.LoadRulesList(_hostRules, _configuration.GetSection("HostRules"));
            CacheRules.LoadRulesList(_cacheRules, _configuration.GetSection("CacheRules"));

            var cacheConfig = _configuration.GetSection("Cache").Get<CacheConfig>();
            cacheConfig ??= new CacheConfig();
            _cacheRepository = new CacheRepository(cacheConfig.DbPath);
            _cacheManager = new CacheManager(cacheConfig.Enabled, cacheConfig.CacheDir, _cacheRepository);
            if (_cacheManager.Enabled)
            {
                _cacheManager.InitAsync().ConfigureAwait(false)
                    .GetAwaiter()
                    .GetResult();
            }

            var proxyMappings = _configuration.GetSection("ProxyMappings").Get<List<ProxyMapping>>();
            var userAgent = _configuration.GetSection("HTTP").GetValue<string>("UserAgent");
            _sslClientOptions = _configuration.GetSection("SslClientOptions").Get<SslClientOptionsConfig>() ?? new SslClientOptionsConfig();
            _sslServerOptions = _configuration.GetSection("SslServerOptions").Get<SslServerOptionsConfig>() ?? new SslServerOptionsConfig();
            var logStep = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogStep") ?? false;

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
                tasks.Add(new ProxyMapper(mapping, _hostRules, _cacheRules, _cacheManager,
                    userAgent, _sslClientOptions, _sslServerOptions, _proxyCounters,
                    _logger, logStep, _maxListenerStartRetries, cancellationToken).Start());
            }
            _started = true;
            _startTime = DateTime.Now;
            Task proxyMappingTasks = Task.WhenAll(tasks);
            proxyMappingTasks.ContinueWith(
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
            return _proxyCounters.SessionsCounter.Count;
        }

        public int GetAuthenticationNotRequired()
        {
            return _proxyCounters.SessionsCounter.AuthenticationNotRequired;
        }
        
        public int GetAuthenticationRequired()
        {
            return _proxyCounters.SessionsCounter.AuthenticationRequired;
        }
        
        public int GetAuthenticated()
        {
            return _proxyCounters.SessionsCounter.Authenticated;
        }
        
        public int GetAuthenticationInvalid()
        {
            return _proxyCounters.SessionsCounter.AuthenticationInvalid;
        }

        public int GetHttpRejected()
        {
            return _proxyCounters.SessionsCounter.HttpRejected;
        }

        public int GetProxyConnected()
        {
            return _proxyCounters.SessionsCounter.ProxyConnected;
        }
        
        public int GetProxyFailed()
        {
            return _proxyCounters.SessionsCounter.ProxyFailed;
        }

        public int GetBypassConnected()
        {
            return _proxyCounters.SessionsCounter.BypassConnected;
        }

        public int GetBypassFailed()
        {
            return _proxyCounters.SessionsCounter.BypassFailed;
        }

        public int GetHeaderFailed()
        {
            return _proxyCounters.SessionsCounter.HeaderFailed;
        }

        public int GetNoHost()
        {
            return _proxyCounters.SessionsCounter.NoHost;
        }

        public int GetHostRejected()
        {
            return _proxyCounters.SessionsCounter.HostRejected;
        }

        public int GetHostProxified()
        {
            return _proxyCounters.SessionsCounter.HostProxified;
        }

        public int GetHostBypassed()
        {
            return _proxyCounters.SessionsCounter.HostBypassed;
        }

        public long GetTotalBytesRead()
        {
            return _proxyCounters.OutgoingReadCounter.TotalBytesRead;
        }

        public long GetTotalBytesSent()
        {
            return _proxyCounters.OutgoingSentCounter.TotalBytesSent;
        }

        public long GetProxyBytesRead()
        {
            return _proxyCounters.OutgoingReadCounter.ProxyBytesRead;
        }

        public long GetProxyBytesSent()
        {
            return _proxyCounters.OutgoingSentCounter.ProxyBytesSent;
        }

        public long GetBypassBytesRead()
        {
            return _proxyCounters.OutgoingReadCounter.BypassBytesRead;
        }

        public long GetBypassBytesSent()
        {
            return _proxyCounters.OutgoingSentCounter.BypassBytesSent;
        }

        public int GetCacheResponses()
        {
            return _proxyCounters.SessionsCounter.CacheResponses;
        }
        
        public long GetCacheBytesSent()
        {
            return _proxyCounters.IncomingSentCounter.CacheBytesSent;
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

// Ignore Spelling: Proxified

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Json;
using ProxyMapService.Counters;
using ProxyMapService.Exceptions;
using ProxyMapService.Interfaces;
using ProxyMapService.Models;
using ProxyMapService.Proxy;
using ProxyMapService.Proxy.Cache;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Handlers;
using ProxyMapService.Proxy.Http2;
using ProxyMapService.Proxy.RoutingLoopDetector;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Tls;
using ProxyMapService.Utils;
using ProxyMapService.WebLogging;
using System.Linq;

namespace ProxyMapService.Services
{
    public class ProxyService : IProxyService
    {
        private readonly string _serviceInfo = $"Service created at {DateTime.Now}";
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly ILogger _sessionLogger;
        private readonly ProxyCounters _proxyCounters;
        private readonly HostsCounter _hostsCounter;
        private readonly BytesLogger _bytesLogger;
        private readonly CertManager _certManager;
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
        private readonly List<PortRange> _listenPorts = [];
        private readonly IRoutingLoopDetector _routingLoopDetector = new RoutingLoopDetector();
        // Kept alive for the whole lifetime of the mapping tasks: sessions lazily read the
        // certificates from it long after StartProxyMappingTasks() has returned.
        // Disposed here when the service is shut down (DI container disposes the singleton),
        // or in StartProxyMappingTasks() when the service is restarted.
        private SslServerOptionsConfig? _sslServerOptions;

        public CancellationToken StoppingToken { 
            get => _stoppingToken; 
            set => _stoppingToken = value;
        }
        
        public bool Started
        {
            get => _started;
        }

        public ProxyService(IConfiguration configuration, ILogger<ProxyService> logger, ILoggerFactory loggerFactory, 
            IHttpTrafficMonitor httpTrafficMonitor, IHttpLoggingSwitch loggingSwitch)
        {
            _configuration = configuration;
            _logger = logger;
            _sessionLogger = loggerFactory.CreateLogger("ProxySession");
            _proxyCounters = new ProxyCounters();
            _hostsCounter = new HostsCounter();
            _bytesLogger = new BytesLogger(_sessionLogger);
            _certManager = new CertManager(_logger);
            var HostStatsEnabled = _configuration.GetSection("HostStats")?.GetValue<bool>("Enabled") ?? false;
            if (HostStatsEnabled)
            {
                _proxyCounters.SessionsCounter.HostProxifiedHandler += _hostsCounter.OnHostConnected;
                _proxyCounters.SessionsCounter.HostBypassedHandler += _hostsCounter.OnHostConnected;
                var HostTrafficStatsEnabled = _configuration.GetSection("HostStats")?.GetValue<bool>("TrafficStats") ?? false;
                if (HostTrafficStatsEnabled)
                {
                    _proxyCounters.OutgoingReadCounter.BytesReadHandler += _hostsCounter.OnBytesRead;
                    _proxyCounters.OutgoingSendCounter.BytesSentHandler += _hostsCounter.OnBytesSent;
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
                _proxyCounters.IncomingSendCounter.LogSending = true;
                _proxyCounters.OutgoingSendCounter.LogSending = true;
            }
            var LogTrafficData = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogTrafficData") ?? false;
            if (LogTrafficData)
            {
                _proxyCounters.OutgoingReadCounter.BytesReadHandler += _bytesLogger.LogBytesRead;
                _proxyCounters.OutgoingSendCounter.BytesSentHandler += _bytesLogger.LogBytesSent;
                _proxyCounters.IncomingReadCounter.BytesReadHandler += _bytesLogger.LogBytesRead;
                _proxyCounters.IncomingSendCounter.BytesSentHandler += _bytesLogger.LogBytesSent;
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
                _proxyCounters.IncomingSendSslCounter.BytesSendHandler += _bytesLogger.LogSslBytesEncoded;
                _proxyCounters.OutgoingSendSslCounter.BytesSendHandler += _bytesLogger.LogSslBytesEncoded;
            }
            var LogHttpRequestHeaders = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogHttpRequestHeaders") ?? false;
            if (LogHttpRequestHeaders)
            {
                _proxyCounters.HttpRequestHeadersLogger ??= new HttpHeadersLogger(null);
                _proxyCounters.HttpRequestHeadersLogger.HttpHeadersHandler += _bytesLogger.LogHttpHeaders;
            }
            var LogHttpResponseHeaders = _configuration.GetSection("DetailedLogging")?.GetValue<bool>("LogHttpResponseHeaders") ?? false;
            if (LogHttpResponseHeaders)
            {
                _proxyCounters.HttpResponseHeadersLogger ??= new HttpHeadersLogger(null);
                _proxyCounters.HttpResponseHeadersLogger.HttpHeadersHandler += _bytesLogger.LogHttpHeaders;
            }
            var monitoringOptions = _configuration.GetSection("WebSocketMonitoring")?.Get<WebSocketMonitoringOptions>();
            if (monitoringOptions != null && monitoringOptions.TrafficMonitor.Enabled)
            {
                _proxyCounters.HttpRequestHeadersLogger ??= new HttpHeadersLogger(loggingSwitch);
                _proxyCounters.HttpRequestHeadersLogger.HttpHeadersHandler += httpTrafficMonitor.LogHttpHeaders;
                _proxyCounters.HttpResponseHeadersLogger ??= new HttpHeadersLogger(loggingSwitch);
                _proxyCounters.HttpResponseHeadersLogger.HttpHeadersHandler += httpTrafficMonitor.LogHttpHeaders;
                _proxyCounters.HttpCompletionLogger ??= new HttpCompletionLogger(loggingSwitch);
                _proxyCounters.HttpCompletionLogger.HttpCompletionHandler += httpTrafficMonitor.LogHttpCompleted;
                if (monitoringOptions?.TrafficMonitor.LogBody == true)
                {
                    _proxyCounters.HttpRequestBodyLogger ??= new HttpBodyLogger(false, loggingSwitch);
                    _proxyCounters.HttpRequestBodyLogger.HttpBodyHandler += httpTrafficMonitor.LogHttpBody;
                    _proxyCounters.HttpResponseBodyLogger ??= new HttpBodyLogger(true, loggingSwitch);
                    _proxyCounters.HttpResponseBodyLogger.HttpBodyHandler += httpTrafficMonitor.LogHttpBody;
                }
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

            // HostRules/ProxyMappings are replaced as a whole by the topmost configuration
            // layer that defines them (arrays must not be merged index by index across layers).
            HostRules.LoadRulesList(_hostRules, ConfigurationTopLayerBinder.GetTopLayerSection(_configuration, "HostRules"));
            CacheRules.LoadRulesList(_cacheRules, _configuration.GetSection("CacheRules").Get<CacheRules>());

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

            var proxyMappings = ConfigurationTopLayerBinder.GetTopLayerSection(_configuration, "ProxyMappings").Get<List<ProxyMapping>>();
            var sessionAPI = _configuration.GetSection("SessionAPI").Get<SessionAPIConfig>() ?? new SessionAPIConfig();
            var httpConfig = _configuration.GetSection("HTTP").Get<HttpConfig>() ?? new HttpConfig();
            var sslClientOptions = _configuration.GetSection("SslClientOptions").Get<SslClientOptionsConfig>() ?? new SslClientOptionsConfig();
            
            _sslServerOptions?.Dispose();
            // Bind via ConfigurationTopLayerBinder so that ServerCertificate/CACertificate are
            // taken only from the topmost configuration layer that defines them, instead of
            // being merged key by key across layers (e.g. Path from appsettings.json mixed
            // with StoreLocation/StoreName/Subject from appsettings.Production.json).
            _sslServerOptions = ConfigurationTopLayerBinder.GetSslServerOptions(_configuration);
            var sslServerOptions = _sslServerOptions;
            // Pre-load the certificates once, single-threaded: they are read lazily per session otherwise.
            try
            {
                _ = sslServerOptions.ServerCertificate.Certificate;
                _ = sslServerOptions.CACertificate.Certificate;
            }
            catch (Exception ex)
            {
                _logger.LogWarning("[ProxyService.{}] Failed to load SSL server options certificates: {ErrorMessage}",
                    _serviceId, ex.Message);
            }

            var detailedLogging = _configuration.GetSection("DetailedLogging");
            Session.LogStepEnabled = detailedLogging.GetValue<bool>("LogStep", false);
            Http2Log.DiagnosticsEnabled = detailedLogging.GetValue<bool>("Http2Logging", false);
            HandlerLogExtensions.ConnectionLogEnabled = detailedLogging.GetValue<bool>("LogConnections", false);
            HandlerLogExtensions.TlsHandshakeLogEnabled = detailedLogging.GetValue<bool>("LogTlsHandshake", false);
            TlsRecordSplitLog.SniSplitDetailedLogEnabled = detailedLogging.GetValue<bool>("LogSniSplitDetails", false);
            HandlerLogExtensions.ProtocolSwitchLogEnabled = detailedLogging.GetValue<bool>("LogProtocolSwitch", false);
            HandlerLogExtensions.ResponseSourceLogEnabled = detailedLogging.GetValue<bool>("LogResponseSource", false);
            HandlerLogExtensions.ClientUserAgentLogEnabled = detailedLogging.GetValue<bool>("LogClientUserAgent", false);

            var clientHelloPeekTimeoutMs = _configuration.GetSection("TlsDetection")?.GetValue<int?>("ClientHelloPeekTimeoutMs") ?? 500;
            if (clientHelloPeekTimeoutMs > 0)
            {
                TlsDetectionConfig.ClientHelloPeekTimeout = TimeSpan.FromMilliseconds(clientHelloPeekTimeoutMs);
            }

            if (proxyMappings == null || proxyMappings.Count == 0)
            {
                _logger.LogWarning("[ProxyService.{}] No proxy mappings", _serviceId);
            }

            _certManager.CreateProxyMapRootCertificate("DO_NOT_TRUST_ProxyMapRoot");

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
            _routingLoopDetector.ClearListenPorts();
            if (proxyMappings != null)
            {
                foreach (var mapping in proxyMappings)
                {
                    for (int port = mapping.Listen.PortRange.Start; port <= mapping.Listen.PortRange.End; port++)
                    {
                        _routingLoopDetector.AddListenPort(port);
                    }
                }
            }
            _routingLoopDetector.InitLocalIpAddresses();
            if (proxyMappings != null)
            {
                foreach (var mapping in proxyMappings)
                {
                    tasks.Add(new ProxyMapper(mapping, _listenPorts, _routingLoopDetector,
                        sessionAPI, _hostRules, _cacheRules, _cacheManager,
                        httpConfig, sslClientOptions, sslServerOptions, _proxyCounters,
                        _logger, _sessionLogger, _maxListenerStartRetries, cancellationToken).Start());
                }
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

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _sslServerOptions?.Dispose();
                _sslServerOptions = null;
            }
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
            return _proxyCounters.OutgoingSendCounter.TotalBytesSent;
        }

        public long GetProxyBytesRead()
        {
            return _proxyCounters.OutgoingReadCounter.ProxyBytesRead;
        }

        public long GetProxyBytesSent()
        {
            return _proxyCounters.OutgoingSendCounter.ProxyBytesSent;
        }

        public long GetBypassBytesRead()
        {
            return _proxyCounters.OutgoingReadCounter.BypassBytesRead;
        }

        public long GetBypassBytesSent()
        {
            return _proxyCounters.OutgoingSendCounter.BypassBytesSent;
        }

        public int GetCacheResponses()
        {
            return _proxyCounters.SessionsCounter.CacheResponses;
        }
        
        public long GetCacheBytesSent()
        {
            return _proxyCounters.IncomingSendCounter.CacheBytesSent;
        }

        public IEnumerable<PortRange> GetListenPorts()
        {
            return _listenPorts;
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

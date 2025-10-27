using Microsoft.Extensions.Logging;
using ProxyMapService.Counters;
using ProxyMapService.Interfaces;
using ProxyMapService.Models;
using ProxyMapService.Proxy;
using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using System.Text;

namespace ProxyMapService.Services
{
    public class ProxyService : IProxyService
    {
        private readonly string _serviceInfo = $"Service created at {DateTime.Now}";
        private readonly List<Task> _tasks = [];
        private readonly IConfiguration _configuration;
        private readonly ILogger _logger;
        private readonly SessionsCounter _sessionsCounter = new();
        private readonly HostsCounter _hostsCounter = new();
        private readonly BytesReadCounter _readCounter = new();
        private readonly BytesSentCounter _sentCounter = new();

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
                    _readCounter.BytesReadHandler += _hostsCounter.OnBytesRead;
                    _sentCounter.BytesSentHandler += _hostsCounter.OnBytesSent;
                }
                var LogTrafficData = _configuration.GetSection("HostStats")?.GetValue<bool>("LogTrafficData") ?? false;
                if (LogTrafficData)
                {
                    _readCounter.BytesReadHandler += LogBytesRead;
                    _sentCounter.BytesSentHandler += LogBytesSent;
                }
            }
            //AddProxyMappingTasks();
        }

        private void LogBytesRead(object? sender, BytesReadEventArgs e)
        {
            var bytesRead = e.BytesRead;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var logData = bytesData == null ? "null" : String.Join("\r\n", Enumerable.Range(0, (bytesRead + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesRead - i * 16, 16))
            ));
            _logger.LogInformation("<<< read {count} byte(s):\r\n{data}", bytesRead, logData);
        }

        private void LogBytesSent(object? sender, BytesSentEventArgs e)
        {
            var bytesSent = e.BytesSent;
            var bytesData = e.BytesData;
            var startIndex = e.StartIndex;
            var logData = bytesData == null ? "null" : String.Join("\r\n", Enumerable.Range(0, (bytesSent + 15) / 16).Select(i =>
                FormatLogData(bytesData, startIndex + i * 16, Math.Min(bytesSent - i * 16, 16))
            ));
            _logger.LogInformation(">>> sent {count} byte(s):\r\n{data}", bytesSent, logData);
        }

        private static string FormatLogData(byte[] data, int startIndex, int length)
        {
            return BitConverter.ToString(data, startIndex, length).Replace("-", " ").PadRight(48, ' ') + " " +
                string.Concat(Encoding.ASCII.GetString(data, startIndex, length).Select(c => c < 32 ? '.' : c));
        }

        /*
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("[ProxyService] Delay...");
            await Task.Delay(-1, stoppingToken);
            _logger.LogInformation("[ProxyService] Delay completed.");
        }
        */

        public void AddProxyMappingTasks(CancellationToken stoppingToken)
        {
            var hostRules = _configuration.GetSection("HostRules").Get<List<HostRule>>();
            var proxyMappings = _configuration.GetSection("ProxyMappings").Get<List<ProxyMapping>>();
            var userAgent = _configuration.GetSection("HTTP").GetValue<string>("UserAgent");
            if (proxyMappings != null)
            {
                foreach (var mapping in proxyMappings)
                {
                    _tasks.Add(new ProxyMapper().Start(mapping, hostRules, userAgent,
                        _sessionsCounter, _readCounter, _sentCounter, _logger, stoppingToken));
                }
            }
        }

        public string GetServiceInfo()
        {
            return _serviceInfo;
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
            return _readCounter.TotalBytesRead;
        }

        public long GetTotalBytesSent()
        {
            return _sentCounter.TotalBytesSent;
        }

        public long GetProxyBytesRead()
        {
            return _readCounter.ProxyBytesRead;
        }

        public long GetProxyBytesSent()
        {
            return _sentCounter.ProxyBytesSent;
        }

        public long GetBypassBytesRead()
        {
            return _readCounter.BypassBytesRead;
        }

        public long GetBypassBytesSent()
        {
            return _sentCounter.BypassBytesSent;
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

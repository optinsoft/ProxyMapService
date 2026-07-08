using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using ProxyMapService.WebLogging;
using System.Threading.Channels;

namespace ProxyMapService.Services
{
    public class WebSocketLogBackgroundService : BackgroundService
    {
        private readonly Channel<WebSocketMessageEntry> _channel;

        private readonly IHubContext<LogHub> _hubContext;
        private readonly ILogger<WebSocketLogBackgroundService> _internalLogger;
        private readonly ILogStorage _logStorage;
        private readonly IHttpTrafficStorage _httpTrafficStorage;
        private readonly IOptionsMonitor<WebSocketMonitoringOptions> _monitoringOptions;

        public WebSocketLogBackgroundService(
            IHubContext<LogHub> hubContext, 
            ILogger<WebSocketLogBackgroundService> internalLogger,
            ILogStorage logStorage,
            IHttpTrafficStorage httpTrafficStorage,
            IOptionsMonitor<WebSocketMonitoringOptions> monitoringOptions)
        {
            _hubContext = hubContext;
            _internalLogger = internalLogger;
            _logStorage = logStorage;
            _httpTrafficStorage = httpTrafficStorage;
            _monitoringOptions = monitoringOptions;

            int capacity = monitoringOptions.CurrentValue.QueueCapacity;

            _channel = Channel.CreateBounded<WebSocketMessageEntry>(new BoundedChannelOptions(capacity)
            {
                FullMode = BoundedChannelFullMode.DropWrite,
                SingleReader = true,
                SingleWriter = false
            });
        }

        public void QueueMessage(WebSocketMessageEntry entry)
        {
            _channel.Writer.TryWrite(entry);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            int maxLogEntries = _monitoringOptions.CurrentValue.EventLog.MaxEntries;
            int maxTrafficEntries = _monitoringOptions.CurrentValue.TrafficMonitor.MaxEntries;

            var parallelOptions = new ParallelOptions
            {
                MaxDegreeOfParallelism = 4,
                CancellationToken = stoppingToken
            };

            var asyncEnumerable = _channel.Reader.ReadAllAsync(stoppingToken);

            await Parallel.ForEachAsync(asyncEnumerable, parallelOptions, async (message, token) =>
            {
                try
                {
                    switch (message)
                    {
                        case LogMessageEntry log:
                            _logStorage.AddLog(log);
                            await _hubContext.Clients.All.SendAsync("EventLog", new EventLogPayload(log, maxLogEntries), token);
                            break;

                        case HttpRequestMessageEntry request:
                            _httpTrafficStorage.AddEntry(request);
                            await _hubContext.Clients.All.SendAsync("HttpRequest", new HttpRequestPayload(request.Dto, maxTrafficEntries), token);
                            break;

                        case HttpResponseMessageEntry response:
                            _httpTrafficStorage.AddEntry(response);
                            await _hubContext.Clients.All.SendAsync("HttpResponse", new HttpResponsePayload(response.Dto, maxTrafficEntries), token);
                            break;

                        case HttpCompletionEntry completion:
                            _httpTrafficStorage.AddEntry(completion);
                            await _hubContext.Clients.All.SendAsync("HttpCompletion", new HttpCompletionPayload(completion.Dto, maxTrafficEntries), token);
                            break;

                        case HttpRequestBodyEntry body:
                            _httpTrafficStorage.AddEntry(body);
                            await _hubContext.Clients.All.SendAsync("HttpRequestBody", new HttpBodyPayload(body.Dto, maxTrafficEntries), token);
                            break;

                        case HttpResponseBodyEntry body:
                            _httpTrafficStorage.AddEntry(body);
                            await _hubContext.Clients.All.SendAsync("HttpResponseBody", new HttpBodyPayload(body.Dto, maxTrafficEntries) , token);
                            break;

                    }
                }
                catch (Exception ex)
                {
                    _internalLogger.LogError(ex, "Error sending log via WebSocket");
                }
            });
        }
    }
}

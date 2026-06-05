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

        public WebSocketLogBackgroundService(
            IHubContext<LogHub> hubContext, 
            ILogger<WebSocketLogBackgroundService> internalLogger,
            IOptions<WebSocketMonitoringOptions> options)
        {
            _hubContext = hubContext;
            _internalLogger = internalLogger;

            int capacity = options.Value.QueueCapacity;

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
            await foreach (var message in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                try
                {
                    switch (message)
                    {
                        case LogMessageEntry log:
                            await _hubContext.Clients.All.SendAsync("EventLog", log, stoppingToken);
                            break;

                        case HttpRequestMessageEntry request:
                            await _hubContext.Clients.All.SendAsync("HttpRequest", request.Dto, stoppingToken);
                            break;

                        case HttpResponseMessageEntry response:
                            await _hubContext.Clients.All.SendAsync("HttpResponse", response.Dto, stoppingToken);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _internalLogger.LogError(ex, "Error sending log via WebSocket");
                }
            }
        }
    }
}

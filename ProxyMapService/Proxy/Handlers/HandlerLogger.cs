using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public static class HandlerLogger
    {
        public static void OnClientDisconnected(object? sender, EventArgs e)
        {
            if (sender is SessionContext context)
            {
                context.Logger.LogClientDisconnected(context.IncomingClient);
            }
        }

        public static void OnBypassServerDisconnected(object? sender, EventArgs e)
        {
            if (sender is SessionContext context)
            {
                context.Logger.LogBypassServerDisconnected(context.OutgoingClient);
            }
        }

        public static void OnProxyServerDisconnected(object? sender, EventArgs e)
        {
            if (sender is SessionContext context)
            {
                context.Logger.LogProxyServerDisconnected(context.OutgoingClient);
            }
        }
    }
}

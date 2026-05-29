using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public static class HandlerLogger
    {
        public static void OnClientDisconnected(object? sender, EventArgs e)
        {
            if (sender is SessionContext context)
            {
                context.Logger.LogClientDisconnected(context.IncomingEndPoint);
            }
        }

        public static void OnBypassServerDisconnected(object? sender, EventArgs e)
        {
            if (sender is SessionContext context)
            {
                context.Logger.LogBypassServerDisconnected(context.OutgoingEndPoint);
            }
        }

        public static void OnProxyServerDisconnected(object? sender, EventArgs e)
        {
            if (sender is SessionContext context)
            {
                context.Logger.LogProxyServerDisconnected(context.OutgoingEndPoint);
            }
        }
    }
}

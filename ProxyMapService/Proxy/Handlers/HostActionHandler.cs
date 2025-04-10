using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class HostActionHandler : IHandler
    {
        private static readonly HostActionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Mapping.Listen.RejectHttpProxy && context.Header?.Verb != "CONNECT")
            {
                context.SessionsCounter?.OnHttpRejected(context);
                await SendMethodNotAllowed(context);
                return HandleStep.Terminate;
            }

            if (context.Header?.Host == null || context.Header.Host.Hostname.Length == 0)
            {
                context.SessionsCounter?.OnNoHost(context);
                await SendBadRequest(context);
                return HandleStep.Terminate;
            }

            context.HostName = context.Header.Host.Hostname;
            context.HostPort = context.Header.Host.Port;

            context.HostAction = GetHostAction(context.HostName, context.HostRules);
            if (context.HostAction == ActionEnum.Deny)
            {
                context.SessionsCounter?.OnHostRejected(context);
                await SendForbidden(context);
                return HandleStep.Terminate;
            }

            return context.HostAction == ActionEnum.Bypass ? HandleStep.Bypass : HandleStep.Proxy;
        }

        private static ActionEnum GetHostAction(string Host, List<HostRule>? hostRules)
        {
            if (hostRules != null)
            {
                foreach (var rule in hostRules)
                {
                    if (rule.Pattern.Match(Host).Success)
                    {
                        return rule.Action;
                    }
                }
            }
            return ActionEnum.Allow;
        }

        public static HostActionHandler Instance()
        {
            return Self;
        }

        private static async Task SendBadRequest(SessionContext context)
        {
            if (context.ClientStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n");
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }

        private static async Task SendForbidden(SessionContext context)
        {
            if (context.ClientStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nConnection: close\r\n\r\n");
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }

        private static async Task SendMethodNotAllowed(SessionContext context)
        {
            if (context.ClientStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 405 Method Not Allowed\r\nConnection: close\r\n\r\n");
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }
    }
}
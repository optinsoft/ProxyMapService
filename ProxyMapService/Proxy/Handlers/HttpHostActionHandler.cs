using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Sessions;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpHostActionHandler : BaseHostActionHandler, IHandler
    {
        private static readonly HttpHostActionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Mapping.Listen.RejectHttpProxy && context.Http?.HTTPVerb != "CONNECT")
            {
                context.SessionsCounter?.OnHttpRejected(context);
                await SendMethodNotAllowed(context);
                return HandleStep.Terminate;
            }

            if (context.Http?.Host == null || context.Http.Host.Hostname.Length == 0)
            {
                context.SessionsCounter?.OnNoHost(context);
                await SendBadRequest(context);
                return HandleStep.Terminate;
            }

            context.HostName = context.Http.Host.Hostname;
            context.HostPort = context.Http.Host.Port;

            GetContextHostAction(context);

            if (context.HostAction == ActionEnum.Deny)
            {
                context.SessionsCounter?.OnHostRejected(context);
                await SendForbidden(context);
                return HandleStep.Terminate;
            }

            return context.HostAction == ActionEnum.Bypass ? HandleStep.HttpBypass : HandleStep.Proxy;
        }

        public static HttpHostActionHandler Instance()
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
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
                await HttpReplyMethodNotAllowed(context);
                return HandleStep.Terminate;
            }

            if (context.Http?.HTTPTargetHost == null || context.Http.HTTPTargetHost.Hostname.Length == 0)
            {
                context.SessionsCounter?.OnNoHost(context);
                await HttpReplyBadRequest(context);
                return HandleStep.Terminate;
            }

            context.HostName = context.Http.HTTPTargetHost.Hostname;
            context.HostPort = context.Http.HTTPTargetHost.Port;

            GetContextHostAction(context);

            switch (context.HostAction)
            {
                case ActionEnum.Allow:
                    return HandleStep.Proxy;
                case ActionEnum.Bypass:
                    return HandleStep.HttpBypass;
                case ActionEnum.File:
                    return HandleStep.HttpFile;
                default:
                    //ActionEnum.Deny
                    context.SessionsCounter?.OnHostRejected(context);
                    await HttpReplyForbidden(context);
                    return HandleStep.Terminate;
            }
        }

        public static HttpHostActionHandler Instance()
        {
            return Self;
        }

        private static async Task HttpReplyBadRequest(SessionContext context)
        {
            if (context.IncomingStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 400 Bad Request\r\nConnection: close\r\n\r\n");
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        private static async Task HttpReplyForbidden(SessionContext context)
        {
            if (context.IncomingStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 403 Forbidden\r\nConnection: close\r\n\r\n");
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        private static async Task HttpReplyMethodNotAllowed(SessionContext context)
        {
            if (context.IncomingStream == null) return;
            var bytes = Encoding.ASCII.GetBytes("HTTP/1.1 405 Method Not Allowed\r\nConnection: close\r\n\r\n");
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }
    }
}
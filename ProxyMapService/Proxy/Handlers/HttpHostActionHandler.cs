using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Network;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpHostActionHandler : BaseHostActionHandler, IHandler
    {
        private static readonly HttpHostActionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Http?.HTTPVerb == "GET" && IsSessionAPIHost(context.SessionAPI, context.Http?.HTTPTargetHost))
            {
                if (context.Http?.HTTPTargetPath == "/session/")
                {
                    return HandleStep.GetSession;
                }
                if (context.Http?.HTTPTargetPath == "/session/reset")
                {
                    return HandleStep.ResetSession;
                }
            }

            if (context.Mapping.Listen.RejectHttpProxy && context.Http?.HTTPVerb != "CONNECT")
            {
                context.Logger.LogHttpForwardingRejected();
                context.ProxyCounters.SessionsCounter?.OnHttpRejected(context);
                await HttpProto.HttpReplyMethodNotAllowed(context);
                return HandleStep.Terminate;
            }

            if (context.Host.Hostname.Length == 0) // context.Host must be initialized in InitializeHandler
            {
                context.Logger.LogNoHost();
                context.ProxyCounters.SessionsCounter?.OnNoHost(context);
                await HttpProto.HttpReplyBadRequest(context);
                return HandleStep.Terminate;
            }

            GetContextHostAction(context);

            if (context.Http?.HTTPVerb != "CONNECT")
            {
                context.SslMode = SslMode.No;
            }

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
                    context.Logger.LogHostRejected(context.Host.Hostname, context.Host.Port);
                    context.ProxyCounters.SessionsCounter?.OnHostRejected(context);
                    await HttpProto.HttpReplyForbidden(context);
                    return HandleStep.Terminate;
            }
        }

        public static HttpHostActionHandler Instance()
        {
            return Self;
        }

        private static bool IsSessionAPIHost(SessionAPIConfig config, HostAddress? host)
        {
            if (!config.Enabled)
            {
                return false;
            }
            if (string.IsNullOrEmpty(config.Domain))
            {
                return host == null || host.Hostname.Length == 0;
            }
            else
            {
                return config.Domain.Equals(host?.Hostname);
            }
        }
    }
}
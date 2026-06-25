using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using static ProxyMapService.Proxy.Utils.HostUtils;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpHostActionHandler : IHandler
    {
        private static readonly HttpHostActionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            var httpMode = context.Http?.HTTPVerb != "CONNECT";

            if (httpMode)
            {
                context.SslMode = SslMode.No;
                // context.Host must be initialized in InitializeHandler
                if (IsSessionAPIHost(context.SessionAPI, context.Host))
                {
                    context.HostAction = SessionActionEnum.SessionAPI;
                    return HandleStep.HttpSessionAPI;
                }
            }

            if (context.Mapping.Listen.RejectHttpProxy && httpMode)
            {
                context.Logger.LogHttpForwardingRejected();
                context.ProxyCounters.SessionsCounter?.OnHttpRejected(context);
                await HttpProto.HttpReplyMethodNotAllowed(context);
                return HandleStep.Terminate;
            }

            // context.Host must be initialized in InitializeHandler
            if (context.Host.Hostname.Length == 0)
            {
                context.Logger.LogNoHost();
                context.ProxyCounters.SessionsCounter?.OnNoHost(context);
                await HttpProto.HttpReplyBadRequest(context);
                return HandleStep.Terminate;
            }

            GetContextHostAction(context, httpMode);

            switch (context.HostAction?.ActionValue)
            {
                case SessionActionEnum.Allow:
                    return HandleStep.Proxy;
                case SessionActionEnum.Bypass:
                    return HandleStep.HttpBypass;
                case SessionActionEnum.File:
                    return HandleStep.HttpFile;
                case SessionActionEnum.SessionAPI:
                    return HandleStep.HttpSessionAPI;
                default:
                    //SessionActionEnum.Deny
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
    }
}
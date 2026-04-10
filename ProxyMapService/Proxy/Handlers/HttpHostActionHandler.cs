using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Handlers
{
    public class HttpHostActionHandler : BaseHostActionHandler, IHandler
    {
        private static readonly HttpHostActionHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.Http?.HTTPVerb == "GET" && context.Http?.HTTPTargetHost == null)
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
                context.ProxyCounters.SessionsCounter?.OnHttpRejected(context);
                await HttpProto.HttpReplyMethodNotAllowed(context);
                return HandleStep.Terminate;
            }

            if (context.Http?.HTTPTargetHost == null || context.Http.HTTPTargetHost.Hostname.Length == 0)
            {
                context.ProxyCounters.SessionsCounter?.OnNoHost(context);
                await HttpProto.HttpReplyBadRequest(context);
                return HandleStep.Terminate;
            }

            context.Host = context.Http.HTTPTargetHost;

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
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using ProxyMapService.Proxy.Proto;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5ConnectRequestHandler : IHandler
    {
        private static readonly Socks5ConnectRequestHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream == null)
            {
                throw new NullClientStreamException();
            }
            byte[]? bytesArray = await Socks5Proto.ReadConnectRequest(context);
            Socks5Status status = context.Socks5?.ParseConnectRequest(bytesArray) ?? Socks5Status.GeneralFailure;
            if (status != Socks5Status.Succeeded)
            {
                context.ProxyCounters.SessionsCounter?.OnSocks5Failure(context);
                await Socks5Proto.Socks5ReplyStatus(context, status);
                return HandleStep.Terminate;
            }

            //return HandleStep.Tunnel;
            return HandleStep.Socks5ConnectRequested;
        }

        public static Socks5ConnectRequestHandler Instance()
        {
            return Self;
        }
    }
}

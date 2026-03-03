using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using System.Net;
using System.Net.Sockets;
using ProxyMapService.Proxy.Proto;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5BypassHandler : IHandler
    {
        private static readonly Socks5BypassHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            context.Bypassed = true;

            context.SessionsCounter?.OnHostBypassed(context);

            try
            {
                IPEndPoint outgoingEndPoint = context.Host.GetIPEndPoint();
                await context.OutgoingClient.ConnectAsync(outgoingEndPoint, context.Token);
            }
            catch (SocketException ex)
            {
                context.SessionsCounter?.OnBypassFailed(context);
                Socks5Status status = ex.SocketErrorCode switch
                {
                    SocketError.ConnectionRefused => Socks5Status.ConnectionRefused,
                    SocketError.HostUnreachable => Socks5Status.HostUnreachable,
                    SocketError.HostNotFound => Socks5Status.HostUnreachable,
                    SocketError.NoData => Socks5Status.HostUnreachable,
                    SocketError.TimedOut => Socks5Status.HostUnreachable,
                    SocketError.TryAgain => Socks5Status.HostUnreachable,
                    SocketError.AddressNotAvailable => Socks5Status.HostUnreachable,
                    SocketError.NetworkUnreachable => Socks5Status.NetworkUnreachable,
                    _ => Socks5Status.GeneralFailure
                };
                await Socks5Proto.Socks5Reply(context, status);
                throw;
            }
            catch (Exception)
            {
                context.SessionsCounter?.OnBypassFailed(context);
                await Socks5Proto.Socks5Reply(context, Socks5Status.GeneralFailure);
                throw;
            }

            context.SessionsCounter?.OnBypassConnected(context);

            context.CreateOutgoingClientStream();

            await Socks5Proto.Socks5Reply(context, Socks5Status.Succeeded);

            return HandleStep.Tunnel;
        }

        public static Socks5BypassHandler Instance()
        {
            return Self;
        }
    }
}

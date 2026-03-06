using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;

namespace ProxyMapService.Proxy.Proto
{
    public class Socks4Proto
    {
        public static async Task Socks4ReplyCommand(SessionContext context, Socks4Command command)
        {
            if (context.IncomingStream == null) return;
            byte[] bytes = [0x0, (byte)command, 0, 0, 0, 0, 0, 0];
            if (context.Socks4 != null)
            {
                Array.Copy(context.Socks4.Bytes, 2, bytes, 2, 6);
            }
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        public static async Task SendSocks4Request(SessionContext context, byte[] requestBytes)
        {
            if (context.OutgoingStream == null) return;
            await context.OutgoingStream.WriteAsync(requestBytes, context.Token);
        }
    }
}

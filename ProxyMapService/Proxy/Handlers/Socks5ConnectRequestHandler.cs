using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;
using ProxyMapService.Proxy.Counters;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5ConnectRequestHandler : IHandler
    {
        private static readonly Socks5ConnectRequestHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.ClientStream == null)
            {
                throw new NullClientStreamException();
            }
            byte[]? bytesArray = await ReadRequest(context.ClientStream, context.Token);
            Socks5Status status = context.Socks5?.ParseConnectRequest(bytesArray) ?? Socks5Status.GeneralFailure;
            if (status != Socks5Status.Succeeded)
            {
                context.SessionsCounter?.OnSocks5Failure(context);
                await SendReply(context, (byte)status);
                return HandleStep.Terminate;
            }

            //return HandleStep.Tunnel;
            return HandleStep.Socks5ConnectRequested;
        }

        public static Socks5ConnectRequestHandler Instance()
        {
            return Self;
        }

        public static async Task<byte[]?> ReadRequest(CountingStream client, CancellationToken token)
        {
            byte[] readBuffer = new byte[1];
            using MemoryStream memoryStream = new();
            byte[] requestBytes = []; 

            client.PauseReadCount();
            try
            {

                int bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                if (readBuffer[0] != 0x05) return null;

                bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                byte atyp = readBuffer[0];
                int alen;

                switch (atyp)
                {
                    case 0x01:
                        alen = 4;
                        break;
                    case 0x03:
                        bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                        if (bytesRead <= 0) return null;
                        memoryStream.Write(readBuffer, 0, bytesRead);
                        alen = (int)readBuffer[0];
                        break;
                    case 0x04:
                        alen = 16;
                        break;
                    default:
                        return null;
                }

                for (int i = 0; i < alen + 2; ++i)
                {
                    bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                    if (bytesRead <= 0) return null;
                    memoryStream.Write(readBuffer, 0, bytesRead);
                }

            } 
            finally
            {
                if ( memoryStream.Length > 0)
                {
                    requestBytes = memoryStream.ToArray();
                    client.OnBytesRead(requestBytes.Length, requestBytes, 0);
                }
                client.ResumeReadCount();
            }

            return requestBytes;
        }

        private static async Task SendReply(SessionContext context, byte reply)
        {
            if (context.ClientStream == null) return;
            byte[] bytes = [0x05, reply, 0x0, 0x01, 0x0, 0x0, 0x0, 0x0, 0x10, 0x10];
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }
    }
}

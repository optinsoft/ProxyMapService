using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Counters;
using System.IO;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5UsernamePasswordHandler : BaseAuthenticationHandler, IHandler
    {
        private static readonly Socks5UsernamePasswordHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream == null)
            {
                throw new NullClientStreamException();
            }
            byte[]? bytesArray = await ReadUsernamePassword(context.IncomingStream, context.Token);
            if (!(context.Socks5?.ParseUsernamePassword(bytesArray) ?? false))
            {
                context.SessionsCounter?.OnSocks5Failure(context);
                await SendNotAuthenticated(context);
                return HandleStep.Terminate;
            }

            if (IsProxyAuthorizationCredentialsCorrect(context))
            {
                OnAuthenticated(context);
                await SendAuthenticated(context);
                return HandleStep.Socks5Authenticated;
            }

            OnAuthenticationInvalid(context);
            await SendNotAuthenticated(context);
            return HandleStep.Terminate;
        }

        public static Socks5UsernamePasswordHandler Instance()
        {
            return Self;
        }
        
        private static bool IsProxyAuthorizationCredentialsCorrect(SessionContext context)
        {
            return context.ProxyAuthenticator.Authenticate(context, context.Socks5?.Username, context.Socks5?.Password);
        }

        private static async Task<byte[]?> ReadUsernamePassword(CountingStream client, CancellationToken token)
        {
            byte[] readBuffer = new byte[1];
            using MemoryStream memoryStream = new();
            byte[] usernamePasswordBytes = [];

            client.PauseReadCount();
            try
            {

                int bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                if (readBuffer[0] != 0x01) return null;

                bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                int ulen = (int)readBuffer[0];

                for (int i = 0; i < ulen; ++i)
                {
                    bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                    if (bytesRead <= 0) return null;
                    memoryStream.Write(readBuffer, 0, bytesRead);
                }

                bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                int plen = (int)readBuffer[0];

                for (int i = 0; i < plen; ++i)
                {
                    bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                    if (bytesRead <= 0) return null;
                    memoryStream.Write(readBuffer, 0, bytesRead);
                }

            }
            finally
            {
                if (memoryStream.Length > 0)
                {
                    usernamePasswordBytes = memoryStream.ToArray();
                    client.OnBytesRead(usernamePasswordBytes.Length, usernamePasswordBytes, 0);
                }
                client.ResumeReadCount();
            }

            return usernamePasswordBytes;
        }

        private static async Task SendAuthenticated(SessionContext context)
        {
            await SendAuthenticationResult(context, 0x00);
        }

        private static async Task SendNotAuthenticated(SessionContext context)
        {
            await SendAuthenticationResult(context, 0x01);
        }

        private static async Task SendAuthenticationResult(SessionContext context, byte result)
        {
            if (context.IncomingStream == null) return;
            byte[] bytes = [0x01, result];
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }
    }
}

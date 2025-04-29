using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Sessions;
using System.IO;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class Socks5UsernamePasswordHandler : IHandler
    {
        private static readonly Socks5UsernamePasswordHandler Self = new();

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.ClientStream == null)
            {
                throw new NullClientStreamException();
            }
            byte[]? bytesArray = await ReadUsernamePassword(context.ClientStream, context.Token);
            if (!(context.Socks5?.ParseUsernamePassword(bytesArray) ?? false))
            {
                context.SessionsCounter?.OnSocks5Failure(context);
                await SendNotAuthenticated(context);
                return HandleStep.Terminate;
            }

            if (!IsVerifyAuthentication(context))
            {
                context.SessionsCounter?.OnAuthenticated(context);
                await SendAuthenticated(context);
                return HandleStep.Socks5Authenticated;
            }

            if (IsProxyAuthorizationCredentialsCorrect(context))
            {
                context.SessionsCounter?.OnAuthenticated(context);
                await SendAuthenticated(context);
                return HandleStep.Socks5Authenticated;
            }

            context.SessionsCounter?.OnAuthenticationInvalid(context);
            await SendNotAuthenticated(context);
            return HandleStep.Terminate;
        }

        public static Socks5UsernamePasswordHandler Instance()
        {
            return Self;
        }
        
        private static bool IsVerifyAuthentication(SessionContext context)
        {
            return context.Mapping.Authentication.Verify;
        }

        private static bool IsProxyAuthorizationCredentialsCorrect(SessionContext context)
        {
            return context.Socks5?.Username == context.Mapping.Authentication.Username && context.Socks5?.Password == context.Mapping.Authentication.Password;
        }

        private static async Task<byte[]?> ReadUsernamePassword(Stream client, CancellationToken token)
        {
            byte[] readBuffer = new byte[1];
            using MemoryStream memoryStream = new();

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

            for (int i = 0; i < ulen; ++i)
            {
                bytesRead = await client.ReadAsync(readBuffer.AsMemory(0, 1), token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);
            }

            return memoryStream.ToArray();
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
            if (context.ClientStream == null) return;
            byte[] bytes = [0x01, result];
            await context.ClientStream.WriteAsync(bytes, context.Token);
        }
    }
}

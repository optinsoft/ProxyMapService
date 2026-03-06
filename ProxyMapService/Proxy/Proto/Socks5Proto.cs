using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Socks;

namespace ProxyMapService.Proxy.Proto
{
    public class Socks5Proto
    {
        public static async Task Socks5ReplyStatus(SessionContext context, Socks5Status status)
        {
            if (context.IncomingStream == null) return;
            byte[] bytes = [0x05, (byte)status, 0x0, 0x01, 0x0, 0x0, 0x0, 0x0, 0x10, 0x10];
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        public static async Task Socks5ReplyNoMethod(SessionContext context)
        {
            await Socks5ReplySelectMethod(context, 0xff);
        }

        public static async Task Socks5ReplySelectMethod(SessionContext context, byte method)
        {
            if (context.IncomingStream == null) return;
            byte[] bytes = [0x05, method];
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        public static async Task Socks5ReplyAuthenticated(SessionContext context)
        {
            await Socks5ReplyAuthenticationResult(context, 0x00);
        }

        public static async Task Socks5ReplyNotAuthenticated(SessionContext context)
        {
            await Socks5ReplyAuthenticationResult(context, 0x01);
        }

        public static async Task Socks5ReplyAuthenticationResult(SessionContext context, byte result)
        {
            if (context.IncomingStream == null) return;
            byte[] bytes = [0x01, result];
            await context.IncomingStream.WriteAsync(bytes, context.Token);
        }

        public static async Task SendSocks5Request(SessionContext context, byte[] requestBytes)
        {
            if (context.OutgoingStream == null) return;
            await context.OutgoingStream.WriteAsync(requestBytes, context.Token);
        }

        public static async Task<byte[]?> ReadConnectRequest(SessionContext context)
        {
            if (context.IncomingStream == null) return null;

            byte[] readBuffer = new byte[1];
            using MemoryStream memoryStream = new();
            byte[] requestBytes = [];

            context.IncomingStream.PauseReadCount();
            try
            {
                int bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                if (readBuffer[0] != 0x05) return null;

                bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
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
                        bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
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
                    bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
                    if (bytesRead <= 0) return null;
                    memoryStream.Write(readBuffer, 0, bytesRead);
                }
            }
            finally
            {
                if (memoryStream.Length > 0)
                {
                    requestBytes = memoryStream.ToArray();
                    context.IncomingStream.OnBytesRead(requestBytes.Length, requestBytes, 0);
                }
                context.IncomingStream.ResumeReadCount();
            }

            return requestBytes;
        }

        public static async Task<byte[]?> ReadUsernamePassword(SessionContext context)
        {
            if (context.IncomingStream == null) return null;

            byte[] readBuffer = new byte[1];
            using MemoryStream memoryStream = new();
            byte[] usernamePasswordBytes = [];

            context.IncomingStream.PauseReadCount();
            try
            {
                int bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                if (readBuffer[0] != 0x01) return null;

                bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                int ulen = (int)readBuffer[0];

                for (int i = 0; i < ulen; ++i)
                {
                    bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
                    if (bytesRead <= 0) return null;
                    memoryStream.Write(readBuffer, 0, bytesRead);
                }

                bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
                if (bytesRead <= 0) return null;
                memoryStream.Write(readBuffer, 0, bytesRead);

                int plen = (int)readBuffer[0];

                for (int i = 0; i < plen; ++i)
                {
                    bytesRead = await context.IncomingStream.ReadAsync(readBuffer.AsMemory(0, 1), context.Token);
                    if (bytesRead <= 0) return null;
                    memoryStream.Write(readBuffer, 0, bytesRead);
                }
            }
            finally
            {
                if (memoryStream.Length > 0)
                {
                    usernamePasswordBytes = memoryStream.ToArray();
                    context.IncomingStream.OnBytesRead(usernamePasswordBytes.Length, usernamePasswordBytes, 0);
                }
                context.IncomingStream.ResumeReadCount();
            }

            return usernamePasswordBytes;
        }

        public static async Task<byte[]?> ReadConnectReply(SessionContext context)
        {
            if (context.OutgoingStream == null) return null;
            int readLength = 4;
            byte[] readBuffer = new byte[readLength];
            int bufferPos = 0, bytesRead;
            byte atyp = 0;
            do
            {
                bytesRead = await context.OutgoingStream.ReadAsync(readBuffer.AsMemory(bufferPos, 1), context.Token);
                if (bytesRead <= 0) return null;
                if (bufferPos == 3)
                {
                    atyp = readBuffer[bufferPos];
                    switch (atyp)
                    {
                        case 0x01:
                            readLength = 10;
                            Array.Resize(ref readBuffer, readLength);
                            break;
                        case 0x03:
                            readLength = 5;
                            Array.Resize(ref readBuffer, readLength);
                            break;
                        case 0x04:
                            readLength = 22;
                            Array.Resize(ref readBuffer, readLength);
                            break;
                    }
                }
                else if (bufferPos == 4 && atyp == 0x03)
                {
                    int alen = (int)readBuffer[bufferPos];
                    readLength = 7 + alen;
                    Array.Resize(ref readBuffer, readLength);
                }
                bufferPos += 1;
            } while (bufferPos < readLength);
            return readBuffer;
        }

        public static async Task<byte[]?> ReadSocks5Reply(SessionContext context, int length)
        {
            if (context.OutgoingStream == null) return null;
            byte[] readBuffer = new byte[length];
            int bufferPos = 0, bytesRead;
            context.OutgoingStream.PauseReadCount();
            try
            {
                do
                {
                    bytesRead = await context.OutgoingStream.ReadAsync(readBuffer.AsMemory(bufferPos, 1), context.Token);
                    if (bytesRead <= 0) return null;
                    bufferPos += 1;
                } while (bufferPos < length);
            }
            finally
            {
                if (bufferPos > 0)
                {
                    context.OutgoingStream.OnBytesRead(bufferPos, readBuffer, 0);
                }
                context.OutgoingStream.ResumeReadCount();
            }
            return readBuffer;
        }
    }
}

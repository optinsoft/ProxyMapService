using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Ssl;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

namespace ProxyMapService.Proxy.Handlers
{
    public class FileRequestHandler : IHandler
    {
        private static readonly FileRequestHandler Self = new();
        private const int BufferSize = 8192;

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && !context.Token.IsCancellationRequested)
            {
                using SslStream? incomingSslStream = context.Ssl ? new(context.IncomingStream) : null;

                string subjectName = $"CN=*.{context.Host.OriginalHostname}";
                X509Certificate2? serverCertificate = context.ServerCertificate;

                if (incomingSslStream != null)
                {
                    if (serverCertificate == null)
                    {
                        if (context.CACertificate != null)
                        {
                            serverCertificate = SslOptionsFactory.CreateSignedCertificate(subjectName, context.Host.OriginalHostname, context.CACertificate);
                        }
                        else
                        {
                            throw new NullServerCertificateException();
                        }
                    }
                    await incomingSslStream.AuthenticateAsServerAsync(SslOptionsFactory.BuildSslServerOptions(context, serverCertificate), context.Token);
                }

                using CountingStream? incomingSslCountingStream = 
                    incomingSslStream != null 
                    ? new CountingStream(incomingSslStream, context, context.IncomingReadSslCounter, context.IncomingSentSslCounter) 
                    : null;

                return await HandleRequest(context, incomingSslCountingStream ?? context.IncomingStream);
            }
            return HandleStep.Terminate;
        }

        public static FileRequestHandler Instance()
        {
            return Self;
        }

        private static async Task<byte[]?> ReadHttpRequest(SessionContext context, Stream incomingStream)
        {
            var buffer = new byte[BufferSize];
            using var ms = new MemoryStream();

            CancellationToken token = context.Token;

            int bytesRead, headersEnd, searchStart = 0;
            do
            {
                bytesRead = await incomingStream.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                if (bytesRead > 0)
                {
                    ms.Write(buffer, 0, bytesRead);
                    if ((headersEnd = HttpParser.FindHeadersEnd(ms, ref searchStart)) >= 0 || searchStart < 0)
                    {
                        var headerBytes = HttpParser.GetHeaderBytes(ms, headersEnd);
                        return headerBytes;
                    }
                }
            } while (bytesRead > 0 && !token.IsCancellationRequested);

            return null;
        }

        private static async Task<HandleStep> HandleRequest(SessionContext context, Stream incomingStream)
        {
            if (context.Http == null || context.Http.HTTPVerb == "CONNECT")
            {
                var headerBytes = await ReadHttpRequest(context, incomingStream);
                if (headerBytes != null)
                {
                    context.Http = new HttpRequestHeader(headerBytes);
                    if (context.Http.BadRequest)
                    {
                        await HttpProto.HttpReplyBadRequest(incomingStream, context.Token);
                        return HandleStep.Terminate;
                    }
                }
                else
                {
                    await HttpProto.HttpReplyBadRequest(incomingStream, context.Token);
                    return HandleStep.Terminate;
                }
            }

            if (context.Http?.HTTPVerb != "GET")
            {
                await HttpProto.HttpReplyMethodNotAllowed(incomingStream, context.Token);
                return HandleStep.Terminate;
            }

            await using var fileStream = OpenFileFromHttpPath(context.RootDir, context.Http?.HTTPTargetPath, ["index.html", "index.htm"]);

            if (fileStream == null)
            {
                await HttpProto.HttpReplyNotFound(incomingStream, context.Token);
                return HandleStep.Terminate;
            }

            await HttpProto.HttpReplyFileStream(incomingStream, context.Token, fileStream);

            return HandleStep.Terminate;
        }

        private static FileStream? OpenFileFromHttpPath(string? rootDir, string? relativePath, List<string> indexFiles)
        {
            // 1. Validate that root directory is provided
            if (string.IsNullOrWhiteSpace(rootDir))
                return null;

            // 2. Validate that relative path is provided
            if (string.IsNullOrWhiteSpace(relativePath))
                return null;

            // 3. Ensure the path starts with a slash (must be a relative path from root)
            //if (!relativePath.StartsWith('/'))
            //    return null;

            // 4. Reject if the path is an absolute URI (e.g., http://example.com, file://, etc.)
            Uri? pathUri = null;
            if (Uri.TryCreate(relativePath, UriKind.Absolute, out pathUri))
                return null;

            // 5. Strip query string (?a=b)
            int queryIndex = relativePath.IndexOf('?');
            if (queryIndex >= 0)
                relativePath = relativePath.Substring(0, queryIndex);

            // 6. URL-decode the path (%20, etc.)
            relativePath = Uri.UnescapeDataString(relativePath);

            // 7. Normalize directory separators
            relativePath = relativePath.Replace('\\', '/');

            // 8. Remove leading slash
            relativePath = relativePath.TrimStart('/');

            // 9. Ensure no segment starts with a dot
            var segments = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            foreach (var segment in segments)
            {
                if (!segment.Equals(".."))
                {
                    if (segment.StartsWith('.'))
                        return null;
                }
            }

            // 10. Combine with the root directory
            string combinedPath = Path.Combine(rootDir, relativePath);

            // 11. Get the full normalized path (resolves ".." segments)
            string fullPath = Path.GetFullPath(combinedPath);

            // 12. Get the full root path for comparison
            string fullRootPath = Path.GetFullPath(rootDir);

            // 13. Verify the resolved path is still within the root directory
            if (!fullPath.StartsWith(fullRootPath, StringComparison.OrdinalIgnoreCase))
                return null;

            // 14. Search index in directory
            if (Directory.Exists(fullPath))
            {
                foreach (var indexFile in indexFiles)
                {
                    string indexPath = Path.Combine(fullPath, indexFile);
                    if (File.Exists(indexPath))
                    {
                        return new FileStream(
                            indexPath,
                            FileMode.Open,
                            FileAccess.Read,
                            FileShare.Read,
                            64 * 1024,
                            useAsync: true);
                    }
                }

                return null; // index not found
            }

            // 15. Check if the file exists
            if (!File.Exists(fullPath))
                return null;

            // 16. Open and return the file stream
            return new FileStream(
                fullPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 64 * 1024,
                useAsync: true);
        }
    }
}

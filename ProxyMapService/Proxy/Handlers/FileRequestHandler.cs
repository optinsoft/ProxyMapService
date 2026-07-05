using ProxyMapService.Proxy.Configurations;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Headers;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Proto;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Ssl;
using System.Net.Security;
using System.Security.Authentication;
using System.Security.Cryptography.X509Certificates;
using static ProxyMapService.Proxy.Utils.HttpBodyUtils;

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
                using SslStream? incomingSslStream = context.SslMode switch
                {
                    SslMode.Yes => new(context.IncomingStream),
                    SslMode.Auto => await context.IncomingStream.IsTLS(context.Token) ? new(context.IncomingStream) : null,
                    _ => null
                };

                string subjectName = $"CN=*.{context.Host.OriginalHostname}";
                X509Certificate2? serverCertificate = context.ServerCertificate;

                if (incomingSslStream != null)
                {
                    using X509Certificate2? tempCertificate = serverCertificate == null && context.CACertificate != null 
                        ? SslOptionsFactory.CreateSignedCertificate(subjectName, context.Host.OriginalHostname, context.CACertificate)
                        : null;
                    serverCertificate ??= tempCertificate ?? throw new NullServerCertificateException();
                    var sslServerOptions = SslOptionsFactory.BuildSslServerOptions(context, serverCertificate);
                    try
                    {
                        await incomingSslStream.AuthenticateAsServerAsync(sslServerOptions, context.Token);
                    }
                    catch (AuthenticationException ex)
                    {
                        context.Logger.LogServerTLSHandshakeFailed(ex.InnerException?.Message ?? ex.Message);
                        return HandleStep.Terminate;
                    }
                    context.Logger.LogServerTLSHandshakeSucceeded();
                }

                using CountingStream? incomingSslCountingStream =
                    incomingSslStream != null
                    ? new CountingStream(incomingSslStream, context,
                        context.ProxyCounters.IncomingReadSslCounter, context.ProxyCounters.IncomingSendSslCounter,
                        context.IncomingStream.ReadTunnelId, context.IncomingStream.SendTunnelId)
                    : null;
                if (incomingSslCountingStream != null)
                {
                    context.IncomingStream.TransferHandlersTo(incomingSslCountingStream);
                }

                var incomingStream = incomingSslCountingStream ?? context.IncomingStream;

                var http = context.Http;
                if (http == null || (incomingSslStream != null && http.HTTPVerb == "CONNECT"))
                {
                    await ReadHttpRequest(context, incomingStream);
                    if (context.RequestHeader != null && !context.RequestHeader.BadRequest)
                    {
                        http = context.RequestHeader;
                    }
                    else
                    {
                        context.Logger.LogHttpBadRequest();
                        await HttpProto.HttpReplyBadRequest(context, incomingStream);
                        return HandleStep.Terminate;
                    }
                }

                return await HandleRequest(context, incomingStream, http);
            }
            return HandleStep.Terminate;
        }

        public static FileRequestHandler Instance()
        {
            return Self;
        }

        private static async Task ReadHttpRequest(SessionContext context, Stream incomingStream)
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
                    if ((headersEnd = HttpParser.FindRequestHeadersEnd(ms, ref searchStart)) >= 0 || searchStart < 0)
                    {
                        var headerBytes = HttpParser.GetRequestHeaderBytes(ms, headersEnd);
                        if (headerBytes != null && headerBytes.Length > 0)
                        {
                            context.RequestHeader = new HttpRequestHeader(headerBytes, context);
                            if (!context.RequestHeader.BadRequest)
                            {
                                CreateRequestBodyTracker(context, null);
                            }
                        }
                        return;
                    }
                }
            } while (bytesRead > 0 && !token.IsCancellationRequested);
        }

        protected virtual async Task<HandleStep> HandleRequest(SessionContext context, Stream incomingStream, HttpRequestHeader http)
        {
            if (http.HTTPVerb != "GET")
            {
                context.Logger.LogHttpMethodNotAllowed(http.HTTPVerb);
                await HttpProto.HttpReplyMethodNotAllowed(context, incomingStream);
                return HandleStep.Terminate;
            }

            await using var fileStream = OpenFileFromHttpPath(context.RootDir, http.HTTPTargetPath, ["index.html", "index.htm"]);

            if (fileStream == null)
            {
                context.Logger.LogHttpFileNotFound(http.HTTPTargetPath);
                await HttpProto.HttpReplyNotFound(context, incomingStream);
                return HandleStep.Terminate;
            }

            await HttpProto.HttpReplyFileStream(context, incomingStream, fileStream);

            context.Logger.LogResponseFromFile(fileStream.Name);

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

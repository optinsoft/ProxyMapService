using Newtonsoft.Json.Linq;
using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Ssl;
using System;
using System.IO;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace ProxyMapService.Proxy.Handlers
{
    public class TunnelHandler : IHandler
    {
        private static readonly TunnelHandler Self = new();
        private const int BufferSize = 8192;
        private static int _tunnelId = 0;

        private class TunnelOptions
        {
            public bool OverrideHost { get; set; }
        }

        public async Task<HandleStep> Run(SessionContext context)
        {
            if (context.IncomingStream != null && context.OutgoingStream != null && !context.Token.IsCancellationRequested)
            {
                using SslStream? incomingSslStream = context.Ssl ? new(context.IncomingStream) : null;
                using SslStream? outgoingSslStream = context.UpstreamSsl ? new(context.OutgoingStream) : null;
                
                string subjectName = $"CN=*.{context.Host.OriginalHostname}";
                X509Certificate2? serverCertificate = context.ServerCertificate;

                if (outgoingSslStream != null)
                {
                    await outgoingSslStream.AuthenticateAsClientAsync(SslOptionsFactory.BuildSslClientOptions(context), context.Token);
                    /*
                    if (outgoingSslStream.RemoteCertificate != null)
                    {
                        var cert = new X509Certificate2(outgoingSslStream.RemoteCertificate);
                        //context.Logger.LogDebug("Server Certificate Subject: {}", cert.Subject);
                        subjectName = cert.Subject;
                    }
                    */
                }

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

                using CountingStream? incomingSslCountingStream = incomingSslStream != null ? new CountingStream(incomingSslStream, context, context.IncomingSslCounter, null) : null;
                using CountingStream? outgoingSslCountingStream = outgoingSslStream != null ? new CountingStream(outgoingSslStream, context, context.OutgoingSslCounter, null) : null;

                await TwoWayTunnel(context, incomingSslCountingStream ?? context.IncomingStream, outgoingSslCountingStream ?? context.OutgoingStream);
            }

            return HandleStep.Terminate;
        }

        public static TunnelHandler Instance()
        {
            return Self;
        }

        private static async Task TwoWayTunnel(SessionContext context, Stream incomingStream, Stream outgoingStream)
        {
            var forwardTask = Tunnel(incomingStream, outgoingStream, context, context.IncomingReadCounter, context.OutgoingSentCounter, 
                new TunnelOptions{
                    OverrideHost = context.Host.Overwritten 
                });
            var reverseTask = Tunnel(outgoingStream, incomingStream, context, context.OutgoingReadCounter, context.IncomingSentCounter);
            await Task.WhenAny(forwardTask, reverseTask);
        }

        private static async Task Tunnel(Stream source, Stream destination, SessionContext context,
            IBytesReadCounter? readCounter, IBytesSentCounter? sentCounter, TunnelOptions? options = null)
        {
            options ??= new TunnelOptions();

            var tunnelId = ++_tunnelId;

            var buffer = new byte[BufferSize];
            using var ms = new MemoryStream();

            CancellationToken token = context.Token;

            try
            {
                int bytesRead, headersEnd, searchStart = 0;
                bool readHeaders = options.OverrideHost;
                do
                {
                    if (readCounter != null && readCounter.IsLogReading)
                    {
                        context.Logger.LogDebug("Tunnel {tunnelId}: reading from {direction}...", tunnelId, StreamDirectionName.GetName(readCounter.Direction));
                    }
                    bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    if (bytesRead > 0)
                    {
                        if (readHeaders)
                        {
                            ms.Write(buffer, 0, bytesRead);
                            if ((headersEnd = HttpParser.FindHeadersEnd(ms, ref searchStart)) >= 0 || searchStart < 0)
                            {
                                readHeaders = false;
                                bool headerModified = false;
                                var headerAndBody = HttpParser.GetHeaderLinesAndBody(ms, headersEnd);
                                if (headerAndBody != null)
                                {
                                    if (options.OverrideHost)
                                    {
                                        if (HttpHostRewriter.OverrideHostHeader(headerAndBody.headerLines, context.Host))
                                        {
                                            headerModified = true;
                                        }
                                    }
                                }
                                if (sentCounter != null && sentCounter.IsLogSending)
                                {
                                    context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...", tunnelId, StreamDirectionName.GetName(sentCounter.Direction));
                                }
                                if (headerAndBody != null && headerModified)
                                {
                                    await SendModifiedHeadersAndBody(destination, headerAndBody.headerLines, headerAndBody.bodyBytes, token);
                                }
                                else
                                {
                                    await destination.WriteAsync(ms.GetBuffer().AsMemory(0, (int)ms.Length), token);
                                }
                            }
                        }
                        else
                        {
                            if (sentCounter != null && sentCounter.IsLogSending)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...", tunnelId, StreamDirectionName.GetName(sentCounter.Direction));
                            }
                            await destination.WriteAsync(buffer.AsMemory(0, bytesRead), token);
                        }
                    }
                } while (bytesRead > 0 && !token.IsCancellationRequested);
            }
            catch (ObjectDisposedException)
            {
                //context.Logger.LogError("ObjectDisposedException: {ErrorMessage}", ex.Message);
            }
        }

        private static async Task SendModifiedHeadersAndBody(Stream destination, string[] headerLines, byte[]? bodyBytes, CancellationToken token)
        {
            string modifiedHeaders = string.Join("\r\n", headerLines);
            byte[] modifiedHeaderBytes = Encoding.ASCII.GetBytes(modifiedHeaders);
            await destination.WriteAsync(modifiedHeaderBytes.AsMemory(0, modifiedHeaderBytes.Length), token);
            if (bodyBytes?.Length > 0)
            {
                await destination.WriteAsync(bodyBytes.AsMemory(0, bodyBytes.Length), token);
            }
        }
    }
}

using ProxyMapService.Proxy.Counters;
using ProxyMapService.Proxy.Exceptions;
using ProxyMapService.Proxy.Http;
using ProxyMapService.Proxy.Sessions;
using ProxyMapService.Proxy.Ssl;
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
            public bool OverrideHost;
        }

        private class TunnelState
        {
            public int TunnelId;
            public bool ResetReadHeaders;
        }

        private class ReverseTunnelOptions
        {
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

                int forwardTunnelId = ++_tunnelId;
                int reverseTunnelId = ++_tunnelId;

                context.IncomingStream.ReadTunnelId = forwardTunnelId;
                context.IncomingStream.SentTunnelId = reverseTunnelId;

                context.OutgoingStream.ReadTunnelId = reverseTunnelId;
                context.OutgoingStream.SentTunnelId = forwardTunnelId;

                using CountingStream? incomingSslCountingStream = 
                    incomingSslStream != null 
                    ? new CountingStream(incomingSslStream, context, context.IncomingReadSslCounter, context.IncomingSentSslCounter, forwardTunnelId, reverseTunnelId) 
                    : null;
                using CountingStream? outgoingSslCountingStream = 
                    outgoingSslStream != null 
                    ? new CountingStream(outgoingSslStream, context, context.OutgoingReadSslCounter, context.OutgoingSentSslCounter, reverseTunnelId, forwardTunnelId) 
                    : null;

                await TwoWayTunnel(context, 
                    incomingSslCountingStream ?? context.IncomingStream, 
                    outgoingSslCountingStream ?? context.OutgoingStream,
                    forwardTunnelId, reverseTunnelId);
            }

            return HandleStep.Terminate;
        }

        public static TunnelHandler Instance()
        {
            return Self;
        }

        private static async Task TwoWayTunnel(SessionContext context, Stream incomingStream, Stream outgoingStream,
            int forwardTunnelId, int reverseTunnelId)
        {
            var forwardTunnelState = new TunnelState
            {
                TunnelId = forwardTunnelId
            };
            var reverseTunnelState = new TunnelState
            {
                TunnelId = reverseTunnelId
            };
            var forwardTask = Tunnel(incomingStream, outgoingStream, context, 
                context.IncomingReadCounter, context.OutgoingSentCounter, 
                forwardTunnelState, reverseTunnelState, 
                new TunnelOptions
                {
                    OverrideHost = context.Host.Overwritten
                });
            var reverseTask = Tunnel(outgoingStream, incomingStream, context, 
                context.OutgoingReadCounter, context.IncomingSentCounter,
                reverseTunnelState, forwardTunnelState);
            await Task.WhenAny(forwardTask, reverseTask);
        }

        private static bool IsReadHeaders(TunnelOptions options)
        {
            return options.OverrideHost;
        }

        private static async Task Tunnel(Stream source, Stream destination, SessionContext context,
            IBytesReadCounter? readCounter, IBytesSentCounter? sentCounter, TunnelState state,
            TunnelState otherTunnelState, TunnelOptions? options = null)
        {
            options ??= new TunnelOptions();

            var buffer = new byte[BufferSize];
            using var ms = new MemoryStream();

            CancellationToken token = context.Token;

            try
            {
                int bytesRead, headersEnd, searchStart = 0;
                bool readHeaders = IsReadHeaders(options);
                do
                {
                    if (readCounter != null && readCounter.IsLogReading)
                    {
                        context.Logger.LogDebug("Tunnel {tunnelId}: reading from {direction}...", state.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                    }
                    bytesRead = await source.ReadAsync(buffer.AsMemory(0, BufferSize), token);
                    if (bytesRead > 0)
                    {
                        if (state.ResetReadHeaders)
                        {
                            state.ResetReadHeaders = false;
                            if (readCounter != null && readCounter.IsLogReading)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: Reset reading headers", state.TunnelId);
                            }
                            if (!readHeaders)
                            {
                                readHeaders = IsReadHeaders(options);
                            }
                        }
                        if (!otherTunnelState.ResetReadHeaders)
                        {
                            if (readCounter != null && readCounter.IsLogReading)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: Resetting other tunnel ({otherTunnelId}) reading headers", state.TunnelId, otherTunnelState.TunnelId);
                            }
                            otherTunnelState.ResetReadHeaders = true;
                        }
                        if (readHeaders)
                        {
                            if (readCounter != null && readCounter.IsLogReading)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: Reading headers from {direction}...", state.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                            }
                            ms.Write(buffer, 0, bytesRead);
                            if ((headersEnd = HttpParser.FindHeadersEnd(ms, ref searchStart)) >= 0 || searchStart < 0)
                            {
                                readHeaders = false;
                                bool headerModified = false;
                                var headerAndBody = HttpParser.GetHeaderLinesAndBody(ms, headersEnd);
                                if (headerAndBody != null)
                                {
                                    if (readCounter != null && readCounter.IsLogReading)
                                    {
                                        context.Logger.LogDebug("Tunnel {tunnelId}: Headers read from {direction}", state.TunnelId, StreamDirectionName.GetName(readCounter.Direction));

                                    }
                                    if (options.OverrideHost)
                                    {
                                        if (headerAndBody.headerLines.Length > 0)
                                        {
                                            var modifiedFirstLine = HttpHeaderRewriter.OverrideHttpCommandHost(headerAndBody.headerLines[0], context.Host);
                                            if (modifiedFirstLine!= null) 
                                            {
                                                headerAndBody.headerLines[0] = modifiedFirstLine;
                                                headerModified = true;
                                            }
                                        }
                                        if (HttpHeaderRewriter.OverrideHostHeader(headerAndBody.headerLines, context.Host))
                                        {
                                            headerModified = true;
                                        }
                                    }
                                }
                                else
                                {
                                    if (readCounter != null && readCounter.IsLogReading)
                                    {
                                        context.Logger.LogDebug("Tunnel {tunnelId}: Body read from {direction}", state.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                                    }
                                }
                                if (sentCounter != null && sentCounter.IsLogSending)
                                {
                                    context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...", state.TunnelId, StreamDirectionName.GetName(sentCounter.Direction));
                                }
                                if (headerAndBody != null && headerModified)
                                {
                                    await SendModifiedHeadersAndBody(destination, headerAndBody.headerLines, headerAndBody.bodyBytes, token);
                                }
                                else
                                {
                                    await destination.WriteAsync(ms.GetBuffer().AsMemory(0, (int)ms.Length), token);
                                }
                                ms.SetLength(0);
                                ms.Position = 0;
                            }
                        }
                        else
                        {
                            if (readCounter != null && readCounter.IsLogReading)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: Body read from {direction}", state.TunnelId, StreamDirectionName.GetName(readCounter.Direction));
                            }
                            if (sentCounter != null && sentCounter.IsLogSending)
                            {
                                context.Logger.LogDebug("Tunnel {tunnelId}: sending to {direction}...", state.TunnelId, StreamDirectionName.GetName(sentCounter.Direction));
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

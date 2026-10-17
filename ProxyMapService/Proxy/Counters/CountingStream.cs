using ProxyMapService.Proxy.Sessions;
using System.Net.Sockets;

namespace ProxyMapService.Proxy.Counters
{
    public class CountingStream(Stream stream, SessionContext context, 
        IBytesReadCounter? readCounter, IBytesSendCounter? sendCounter,
        long readTunnelId, long sendTunnelId) : Stream
    {
        private bool _readCountPaused = false;
        private bool _sendCountPaused = false;
        //private bool? _isTls = null;
        private byte[]? _tlsRecord;
        private bool _tlsPeeked;

        public long ReadTunnelId { get; set; } = readTunnelId;
        public long SendTunnelId { get; set; } = sendTunnelId;

        public event EventHandler? DisconnectHandler;

        public void TransferHandlersTo(CountingStream target)
        {
            if (target == null) return;
            target.DisconnectHandler = this.DisconnectHandler;
            this.DisconnectHandler = null;
        }

        public virtual void OnBytesRead(int bytesRead, byte[]? bytesData, int startIndex)
        {
            readCounter?.OnBytesRead(context, bytesRead, bytesData, startIndex, ReadTunnelId);
        }

        public virtual void OnBytesSend(int bytesSent, byte[]? bytesData, int startIndex)
        {
            sendCounter?.OnBytesSend(context, bytesSent, bytesData, startIndex, SendTunnelId);
        }

        public virtual void OnBytesSent(int bytesSent, byte[]? bytesData, int startIndex)
        {
            sendCounter?.OnBytesSent(context, bytesSent, bytesData, startIndex, SendTunnelId);
        }

        public virtual void OnDisconnected()
        {
            DisconnectHandler?.Invoke(context, EventArgs.Empty);
            DisconnectHandler = null;
        }

        public void PauseReadCount() { _readCountPaused = true; }
        public void PauseSendCount() { _sendCountPaused = true; }
        public void ResumeReadCount() { _readCountPaused = false; }
        public void ResumeSendCount() { _sendCountPaused = false; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (!buffer.IsEmpty)
            {
                if (!_readCountPaused)
                {
                    readCounter?.OnBytesRead(context, bytesRead, buffer.ToArray(), 0, ReadTunnelId);
                }
                if (bytesRead == 0)
                {
                    OnDisconnected();
                }
            }
            return bytesRead;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!buffer.IsEmpty && !_sendCountPaused)
            {
                sendCounter?.OnBytesSend(context, buffer.Length, buffer.ToArray(), 0, SendTunnelId);
            }
            await stream.WriteAsync(buffer, cancellationToken);
            if (!buffer.IsEmpty && !_sendCountPaused)
            {
                sendCounter?.OnBytesSent(context, buffer.Length, buffer.ToArray(), 0, SendTunnelId);
            }
        }

        public override bool CanRead => stream.CanRead;
        public override bool CanSeek => stream.CanSeek;
        public override bool CanWrite => stream.CanWrite;
        public override long Length => stream.Length;
        public override long Position { get => stream.Position; set => stream.Position = value; }
        public override void Flush() => stream.Flush();
        public override int Read(byte[] buffer, int offset, int count) =>
            stream.Read(buffer, offset, count);
        public override long Seek(long offset, SeekOrigin origin) =>
            stream.Seek(offset, origin);
        public override void SetLength(long value) => stream.SetLength(value);
        public override void Write(byte[] buffer, int offset, int count) =>
            stream.Write(buffer, offset, count);

        protected override void Dispose(bool disposing)
        {
            if (disposing) stream.Dispose();
            base.Dispose(disposing);
        }
        /*
        public async Task<bool> IsTLS(CancellationToken cancellationToken = default)
        {
            if (_isTls != null)
            {
                return _isTls.Value;
            }

            if (stream is not NetworkStream networkStream)
            {
                return false;
            }

            Socket socket = networkStream.Socket;
            byte[] peekBuffer = new byte[1];

            int bytesRead = await socket.ReceiveAsync(
                new ArraySegment<byte>(peekBuffer),
                SocketFlags.Peek,
                cancellationToken
            );

            if (bytesRead < 1 || peekBuffer[0] != 0x16)
            {
                _isTls = false;
            }
            else
            {
                peekBuffer = new byte[2];

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(500));

                try
                {
                    bytesRead = await socket.ReceiveAsync(
                       new ArraySegment<byte>(peekBuffer),
                        SocketFlags.Peek,
                        timeoutCts.Token
                    );

                    _isTls = bytesRead >= 2 && peekBuffer[0] == 0x16 && peekBuffer[1] == 0x03;
                }
                catch (OperationCanceledException) when (
                    timeoutCts.IsCancellationRequested &&
                    !cancellationToken.IsCancellationRequested)
                {
                    _isTls = false;
                }
            }

            return _isTls.Value;
        }
        */
        public async Task<byte[]?> PeekTlsRecordAsync(TimeSpan timeout, CancellationToken ct = default)
        {
            if (_tlsPeeked) return _tlsRecord;
            _tlsRecord = await PeekCoreAsync(timeout, ct);
            _tlsPeeked = true;
            return _tlsRecord;
        }

        public async Task<bool> IsTLS(CancellationToken ct = default)
            => await PeekTlsRecordAsync(TimeSpan.FromMilliseconds(500), ct) != null;

        private async Task<byte[]?> PeekCoreAsync(TimeSpan timeout, CancellationToken ct)
        {
            if (stream is not NetworkStream ns) return null;
            var socket = ns.Socket;

            var first = new byte[1];
            int n = await socket.ReceiveAsync(first, SocketFlags.Peek, ct);
            if (n == 0 || first[0] != 0x16) return null;

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            try
            {
                var hdr = new byte[5];
                await PeekExactAsync(socket, hdr, cts.Token);
                if (hdr[1] != 0x03) return null;
                var rec = new byte[5 + ((hdr[3] << 8) | hdr[4])];
                await PeekExactAsync(socket, rec, cts.Token);
                return rec;
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                return null;
            }
        }

        private static async Task PeekExactAsync(Socket s, byte[] buf, CancellationToken ct)
        {
            while (true)
            {
                int n = await s.ReceiveAsync(buf, SocketFlags.Peek, ct);
                if (n == 0) throw new IOException("Connection closed during peek");
                if (n >= buf.Length) return;
                await Task.Delay(2, ct);
            }
        }
    }
}

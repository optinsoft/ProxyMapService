using ProxyMapService.Proxy.Sessions;

namespace ProxyMapService.Proxy.Counters
{
    public class CountingStream(Stream stream, SessionContext context, 
        IBytesReadCounter? readCounter, IBytesSendCounter? sendCounter,
        long readTunnelId, long sendTunnelId) : Stream
    {
        private bool _readCountPaused = false;
        private bool _sendCountPaused = false;

        public long ReadTunnelId { get; set; } = readTunnelId;
        public long SendTunnelId { get; set; } = sendTunnelId;

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

        public void PauseReadCount() { _readCountPaused = true; }
        public void PauseSendCount() { _sendCountPaused = true; }
        public void ResumeReadCount() { _readCountPaused = false; }
        public void ResumeSendCount() { _sendCountPaused = false; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (!_readCountPaused)
            {
                readCounter?.OnBytesRead(context, bytesRead, buffer.ToArray(), 0, ReadTunnelId);
            }
            return bytesRead;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_sendCountPaused)
            {
                sendCounter?.OnBytesSend(context, buffer.Length, buffer.ToArray(), 0, SendTunnelId);
            }
            await stream.WriteAsync(buffer, cancellationToken);
            if (!_sendCountPaused)
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
    }
}

using ProxyMapService.Proxy.Sessions;
using System;
using System.Net.Sockets;
using System.Reflection;

namespace ProxyMapService.Proxy.Counters
{
    public class CountingStream(Stream stream, SessionContext context, IBytesReadCounter? readCounter, IBytesSentCounter? sentCounter) : Stream
    {
        private bool _readCountPaused = false;
        private bool _sentCountPaused = false;

        public virtual void OnBytesRead(int bytesRead, byte[]? bytesData, int startIndex)
        {
            readCounter?.OnBytesRead(context, bytesRead, bytesData, startIndex);
        }

        public virtual void OnBytesSent(int bytesSent, byte[]? bytesData, int startIndex)
        {
            sentCounter?.OnBytesSent(context, bytesSent, bytesData, startIndex);
        }

        public void PauseReadCount() { _readCountPaused = true; }
        public void PauseSentCount() { _sentCountPaused = true; }
        public void ResumeReadCount() { _readCountPaused = false; }
        public void ResumeSentCount() { _sentCountPaused = false; }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            var bytesRead = await stream.ReadAsync(buffer, cancellationToken);
            if (!_readCountPaused)
            {
                readCounter?.OnBytesRead(context, bytesRead, buffer.ToArray(), 0);
            }
            return bytesRead;
        }

        public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            await stream.WriteAsync(buffer, cancellationToken);
            if (!_sentCountPaused)
            {
                sentCounter?.OnBytesSent(context, buffer.Length, buffer.ToArray(), 0);
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

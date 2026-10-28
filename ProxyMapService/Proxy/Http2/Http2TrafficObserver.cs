using System.Buffers.Binary;

namespace ProxyMapService.Proxy.Http2
{
    public enum H2Direction { ClientToServer, ServerToClient }

    /// <summary>
    /// Passive HTTP/2 parsing: bytes are forwarded by tunnels as is, while a copy of them ends up here.
    /// The observer does not write anything in either direction, so a parsing error does not break the connection.
    /// Two HPACK decoders (one per direction) must see ALL header blocks in order.
    /// </summary>
    public sealed class Http2TrafficObserver
    {
        /// <summary>The upper limit of the dynamic table that we are willing to accept in SIZE UPDATE (Chrome/Firefox use 64 KB).</summary>
        public const int PassiveMaxDynamicTableSize = 128 * 1024;

        private const int MaxHeaderBlock = 256 * 1024;
        private const int MaxTrackedStreams = 2048;
        private const int MaxFrameSize = (1 << 24) - 1; // upper limit per RFC 9113: the peer could have negotiated more than 16 KB

        private sealed class Dir(H2Direction direction, IHpackDecoder hpack, int prefaceLength)
        {
            public readonly H2Direction Direction = direction;
            public readonly IHpackDecoder Hpack = hpack;
            public readonly H2FrameReader Reader = new(MaxFrameSize);
            public readonly MemoryStream Block = new();
            public int PrefaceLeft = prefaceLength;
            public bool Failed, InContinuation, BlockEndStream;
            public int BlockStream, BlockPromised;
        }

        private readonly object _sync = new();
        private readonly Http2TrafficSink _sink;
        private readonly Dir _request;
        private readonly Dir _response;
        private readonly Dictionary<int, Http2StreamInfo> _streams = new();
        private bool _closed;

        /// <param name="requestDecoder">The client header decoder (client → server).</param>
        /// <param name="responseDecoder">The server header decoder (server → client). Must be a separate instance.</param>
        public Http2TrafficObserver(Http2TrafficSink sink, IHpackDecoder requestDecoder, IHpackDecoder responseDecoder)
        {
            _sink = sink;
            _request = new Dir(H2Direction.ClientToServer, requestDecoder, H2.Preface.Length);
            _response = new Dir(H2Direction.ServerToClient, responseDecoder, 0);
        }

        /// <summary>Passes a copy of the direction bytes to the observer. Does not throw parsing exceptions.</summary>
        public void Feed(H2Direction direction, ReadOnlySpan<byte> data)
        {
            var d = direction == H2Direction.ClientToServer ? _request : _response;
            lock (_sync)
            {
                if (_closed || d.Failed) return;
                try
                {
                    FeedCore(d, data);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    d.Failed = true;
                    try { _sink.OnObserverFailed(d.Direction, ex); }
                    catch { /* the recipient must not break the tunnel */ }
                }
            }
        }

        /// <summary>Close all open streams and release resources.</summary>
        public void Close()
        {
            lock (_sync)
            {
                if (_closed) return;
                _closed = true;
                foreach (var s in _streams.Values)
                {
                    try { _sink.OnStreamCompleted(s); }
                    catch { /* the recipient must not break the tunnel */ }
                }
                _streams.Clear();
                _request.Block.Dispose();
                _response.Block.Dispose();
            }
        }

        private void FeedCore(Dir d, ReadOnlySpan<byte> data)
        {
            if (d.PrefaceLeft > 0)
            {
                int n = Math.Min(d.PrefaceLeft, data.Length);
                var expected = H2.Preface.AsSpan(H2.Preface.Length - d.PrefaceLeft, n);
                if (!data[..n].SequenceEqual(expected))
                    throw new InvalidDataException("Invalid HTTP/2 client preface");
                d.PrefaceLeft -= n;
                data = data[n..];
            }
            if (data.IsEmpty) return;

            d.Reader.Append(data);
            while (d.Reader.TryRead(out var frame))
                HandleFrame(d, frame);
        }

        private void HandleFrame(Dir d, H2Frame f)
        {
            if (d.InContinuation)
            {
                if (f.Type != H2.Continuation || f.StreamId != d.BlockStream)
                    throw new InvalidDataException("CONTINUATION expected");
            }
            else if (f.Type == H2.Continuation)
            {
                throw new InvalidDataException("Unexpected CONTINUATION");
            }

            switch (f.Type)
            {
                case H2.Headers:
                case H2.PushPromise:
                case H2.Continuation:
                    OnHeaderFrame(d, f);
                    break;
                case H2.Data:
                    OnData(d, f);
                    break;
                case H2.RstStream:
                    OnRst(d, f);
                    break;
                case H2.GoAway:
                    OnGoAway(d, f);
                    break;
                    // SETTINGS, PING, WINDOW_UPDATE, PRIORITY: not needed for logging headers and bodies
            }
        }

        // ---------------- HEADERS / PUSH_PROMISE / CONTINUATION ----------------

        private void OnHeaderFrame(Dir d, H2Frame f)
        {
            var p = f.Payload.AsSpan();
            if (f.Type != H2.Continuation)
            {
                if (f.StreamId == 0) throw new InvalidDataException("HEADERS/PUSH_PROMISE on stream 0");
                if (f.Type == H2.PushPromise && d.Direction == H2Direction.ClientToServer)
                    throw new InvalidDataException("PUSH_PROMISE from the client");

                int pad = 0;
                if ((f.Flags & H2.Padded) != 0)
                {
                    if (p.IsEmpty) throw new InvalidDataException("Bad padding");
                    pad = p[0];
                    p = p[1..];
                }
                if (f.Type == H2.Headers && (f.Flags & H2.PriorityFlag) != 0)
                {
                    if (p.Length < 5) throw new InvalidDataException("Bad HEADERS priority");
                    p = p[5..];
                }
                int promised = 0;
                if (f.Type == H2.PushPromise)
                {
                    if (p.Length < 4) throw new InvalidDataException("Bad PUSH_PROMISE");
                    promised = (int)(BinaryPrimitives.ReadUInt32BigEndian(p) & 0x7FFFFFFF);
                    p = p[4..];
                }
                if (pad > p.Length) throw new InvalidDataException("Bad padding");
                p = p[..(p.Length - pad)];

                d.Block.SetLength(0);
                d.BlockStream = f.StreamId;
                d.BlockPromised = promised;
                d.BlockEndStream = f.Type == H2.Headers && (f.Flags & H2.EndStream) != 0;
            }

            d.Block.Write(p);
            if (d.Block.Length > MaxHeaderBlock) throw new InvalidDataException("Header block is too large");
            if ((f.Flags & H2.EndHeaders) == 0)
            {
                d.InContinuation = true;
                return;
            }
            d.InContinuation = false;

            // always decode, even for a foreign/unknown stream: otherwise the dynamic table will go out of sync
            var headers = d.Hpack.Decode(d.Block.GetBuffer().AsSpan(0, (int)d.Block.Length));
            d.Block.SetLength(0);

            if (d.BlockPromised != 0) OnPushPromise(d.BlockPromised, headers);
            else OnHeaders(d, d.BlockStream, headers, d.BlockEndStream);
        }

        private void OnHeaders(Dir d, int id, List<H2Header> headers, bool end)
        {
            if (d.Direction == H2Direction.ClientToServer)
            {
                if (!_streams.TryGetValue(id, out var s)) s = AddStream(id, false);
                if (s.RequestHeaders == null)
                {
                    s.RequestHeaders = headers;
                    _sink.OnRequestHeaders(s);
                }
                else
                {
                    s.RequestTrailers = headers;
                    _sink.OnRequestTrailers(s);
                }
                if (end) EndRequest(s);
                return;
            }

            if (!_streams.TryGetValue(id, out var stream)) return; // unknown stream: the block has already been decoded
            if (stream.ResponseHeaders == null)
            {
                int status = ParseStatus(headers);
                if (status is >= 100 and < 200) return; // informational response (100, 103): waiting for the final one
                stream.Status = status;
                stream.ResponseHeaders = headers;
                _sink.OnResponseHeaders(stream);
            }
            else
            {
                stream.ResponseTrailers = headers;
                _sink.OnResponseTrailers(stream);
            }
            if (end) EndResponse(stream);
        }

        private void OnPushPromise(int promisedId, List<H2Header> headers)
        {
            var s = AddStream(promisedId, true);
            s.RequestHeaders = headers;
            _sink.OnRequestHeaders(s);
            EndRequest(s); // the request sent by the server has no body
        }

        private static int ParseStatus(List<H2Header> headers)
        {
            foreach (var h in headers)
                if (h.Name == ":status" && int.TryParse(h.Value, out var status)) return status;
            return 0;
        }

        // ---------------- DATA ----------------

        private void OnData(Dir d, H2Frame f)
        {
            if (f.StreamId == 0) throw new InvalidDataException("DATA on stream 0");
            var p = f.Payload.AsSpan();
            if ((f.Flags & H2.Padded) != 0)
            {
                if (p.IsEmpty) throw new InvalidDataException("Bad padding");
                int pad = p[0];
                if (pad > p.Length - 1) throw new InvalidDataException("Bad padding");
                p = p.Slice(1, p.Length - 1 - pad);
            }
            bool end = (f.Flags & H2.EndStream) != 0;
            if (!_streams.TryGetValue(f.StreamId, out var s)) return;

            if (d.Direction == H2Direction.ClientToServer)
            {
                s.RequestBytes += p.Length;
                if (p.Length > 0) _sink.OnRequestData(s, p);
                if (end) EndRequest(s);
            }
            else
            {
                if (s.ResponseHeaders == null) return;
                s.ResponseBytes += p.Length;
                if (p.Length > 0) _sink.OnResponseData(s, p);
                if (end) EndResponse(s);
            }
        }

        // ---------------- RST_STREAM / GOAWAY ----------------

        private void OnRst(Dir d, H2Frame f)
        {
            if (f.StreamId == 0 || f.Payload.Length != 4) throw new InvalidDataException("Bad RST_STREAM");
            uint code = BinaryPrimitives.ReadUInt32BigEndian(f.Payload);
            if (!_streams.Remove(f.StreamId, out var s)) return;
            s.Reset = true;
            _sink.OnStreamReset(s, d.Direction, code);
            _sink.OnStreamCompleted(s);
        }

        private void OnGoAway(Dir d, H2Frame f)
        {
            if (f.Payload.Length < 8) throw new InvalidDataException("Bad GOAWAY");
            int last = (int)(BinaryPrimitives.ReadUInt32BigEndian(f.Payload) & 0x7FFFFFFF);
            uint code = BinaryPrimitives.ReadUInt32BigEndian(f.Payload.AsSpan(4));
            _sink.OnGoAway(d.Direction, last, code);
        }

        // ---------------- streams ----------------

        private Http2StreamInfo AddStream(int id, bool pushed)
        {
            if (_streams.Count >= MaxTrackedStreams)
            {
                int victim = 0;
                foreach (var key in _streams.Keys) { victim = key; break; }
                if (_streams.Remove(victim, out var old)) _sink.OnStreamCompleted(old);
            }
            var s = new Http2StreamInfo(id, pushed);
            _streams[id] = s;
            return s;
        }

        private void EndRequest(Http2StreamInfo s)
        {
            if (s.RequestEnded) return;
            s.RequestEnded = true;
            _sink.OnRequestEnd(s);
            TryComplete(s);
        }

        private void EndResponse(Http2StreamInfo s)
        {
            if (s.ResponseEnded) return;
            s.ResponseEnded = true;
            _sink.OnResponseEnd(s);
            TryComplete(s);
        }

        private void TryComplete(Http2StreamInfo s)
        {
            if (!s.RequestEnded || !s.ResponseEnded) return;
            _streams.Remove(s.StreamId);
            _sink.OnStreamCompleted(s);
        }
    }
}

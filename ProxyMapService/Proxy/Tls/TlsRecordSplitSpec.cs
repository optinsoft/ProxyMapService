namespace ProxyMapService.Proxy.Tls
{
    /// <summary>
    /// Specification for splitting the first outgoing TLS record (the ClientHello) into
    /// several separate TLS records on the TLS protocol level itself. The resulting records
    /// carry the same content type and version as the original record and together
    /// reassemble into the identical payload, so any RFC-conforming TLS server accepts them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Positions use the syntax <c>offset[:repeats:skip][+flag1[flag2]]</c>,
    /// measured from the start of the ClientHello itself (the record payload, i.e. without
    /// the 5-byte TLS record header). Examples:
    /// <c>1</c>, <c>3+s</c> (3 bytes after the start of the SNI hostname),
    /// <c>0+sm</c> (middle of the SNI hostname), <c>-5</c> (5 bytes from the end).
    /// </para>
    /// <para>Flags: <c>+s</c> — SNI start offset, <c>+e</c> — end offset,
    /// <c>+m</c> — middle offset, <c>+n</c> — zero (absolute) offset.
    /// When <c>+s</c> is combined with <c>+e</c> or <c>+m</c>, the end/middle of the
    /// SNI hostname is used. Multiple positions are separated by commas/semicolons.
    /// </para>
    /// <para>
    /// Note: this kind of splitting operates on the TLS record layer — it is applied by the
    /// proxy itself on the outgoing connection and works on any platform.
    /// </para>
    /// </remarks>
    public sealed class TlsRecordSplitSpec
    {
        private sealed record SplitPoint(int Offset, int Repeats, int Skip, string Flags);

        private readonly List<SplitPoint> _points;

        private TlsRecordSplitSpec(List<SplitPoint> points)
        {
            _points = points;
        }

        /// <summary>
        /// Parses a split position list. Returns null when the value is empty
        /// (splitting disabled). Throws <see cref="FormatException"/> on invalid syntax.
        /// </summary>
        public static TlsRecordSplitSpec? Parse(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var points = new List<SplitPoint>();
            foreach (var raw in value.Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                points.Add(ParsePoint(raw));
            }

            return points.Count > 0 ? new TlsRecordSplitSpec(points) : null;
        }

        private static SplitPoint ParsePoint(string token)
        {
            var flags = string.Empty;
            int plus = token.IndexOf('+');
            if (plus >= 0)
            {
                flags = token[(plus + 1)..];
                token = token[..plus];
            }

            var parts = token.Split(':');
            if (!int.TryParse(parts[0], out int offset))
            {
                throw new FormatException($"Invalid TLS record split offset '{parts[0]}'.");
            }

            int repeats = parts.Length > 1 ? int.Parse(parts[1]) : 1;
            int skip = parts.Length > 2 ? int.Parse(parts[2]) : 1;
            if (repeats < 1 || skip < 1)
            {
                throw new FormatException($"Invalid TLS record split repeats/skip in '{token}'.");
            }

            if (flags.Any(c => !"senm".Contains(char.ToLowerInvariant(c))))
            {
                throw new FormatException($"Invalid TLS record split flags '+{flags}'.");
            }

            return new SplitPoint(offset, repeats, skip, flags.ToLowerInvariant());
        }

        /// <summary>
        /// Splits one complete TLS record (5-byte header included) into several records
        /// with the payload re-chunked at the configured positions. Positions are relative
        /// to the record payload (the ClientHello bytes, without the 5-byte record header).
        /// Returns the concatenated result, or null when the spec resolves to nothing to split
        /// (e.g. all positions reference an SNI that is absent from the record).
        /// </summary>
        /// <param name="record">The complete TLS record, header included.</param>
        /// <param name="sniStart">Absolute offset of the SNI hostname inside the record, if present.</param>
        /// <param name="sniEnd">Absolute end offset of the SNI hostname inside the record, if present.</param>
        public byte[]? BuildSplitRecords(ReadOnlySpan<byte> record, int? sniStart, int? sniEnd)
        {
            var segments = BuildSplitRecordSegments(record, sniStart, sniEnd);
            if (segments == null) return null;
            var result = new byte[segments.Sum(s => s.Length)];
            int target = 0;
            foreach (var segment in segments)
            {
                segment.CopyTo(result, target);
                target += segment.Length;
            }
            return result;
        }

        /// <summary>
        /// Same as <see cref="BuildSplitRecords"/>, but returns each resulting TLS record as a
        /// separate segment so that they can be written (and sent) individually — a separate
        /// write per split record so that DPI sees them as distinct packets.
        /// </summary>
        public List<byte[]>? BuildSplitRecordSegments(ReadOnlySpan<byte> record, int? sniStart, int? sniEnd)
        {
            int recordLen = record.Length;
            int payloadLen = recordLen - 5;
            if (payloadLen <= 0) return null;

            // SNI offsets are record-relative — convert them to payload coordinates.
            int? sniPos = sniStart.HasValue ? sniStart.Value - 5 : null;
            int? sniEndPos = sniEnd.HasValue ? sniEnd.Value - 5 : null;

            var positions = new List<int>();
            foreach (var point in _points)
            {
                for (int i = 0; i < point.Repeats; i++)
                {
                    int pos = ResolvePosition(point, i, payloadLen, sniPos, sniEndPos);
                    if (pos > 0 && pos < payloadLen)
                    {
                        positions.Add(pos);
                    }
                }
            }

            if (positions.Count == 0)
            {
                return null;
            }

            // positions may come in any order and with duplicates: sort and dedupe
            var offsets = positions.Distinct().OrderBy(p => p).ToList();

            // splits produce offsets.Count + 1 records: the first keeps the original header
            var result = new List<byte[]>(offsets.Count + 1);
            int source = 0;
            foreach (int end in offsets)
            {
                result.Add(WriteRecord(record, source, end));
                source = end;
            }
            result.Add(WriteRecord(record, source, payloadLen));
            return result;
        }

        /// <summary>Builds one TLS record (5-byte header + payload chunk) from the source record.</summary>
        private static byte[] WriteRecord(ReadOnlySpan<byte> record, int source, int end)
        {
            int length = end - source;
            var result = new byte[5 + length];
            record.Slice(0, 5).CopyTo(result);
            result[3] = (byte)(length >> 8);
            result[4] = (byte)length;
            // The payload starts after the 5-byte record header.
            record.Slice(5 + source, length).CopyTo(result.AsSpan(5));
            return result;
        }

        private static int ResolvePosition(SplitPoint point, int iteration, int payloadLen, int? sniPos, int? sniEndPos)
        {
            bool hasSni = sniPos.HasValue && sniEndPos.HasValue;
            int pos = point.Offset + (point.Repeats > 1 ? iteration * point.Skip : 0);

            // A negative offset without flags counts from the end of the message.
            if (point.Offset < 0 && point.Flags.Length == 0)
            {
                pos += payloadLen;
            }

            foreach (var flag in point.Flags)
            {
                switch (flag)
                {
                    case 'n':
                        break; // zero offset — the position stays absolute
                    case 's':
                        if (!hasSni) return -1;
                        pos += sniPos!.Value;
                        break;
                    case 'e':
                        if (point.Flags.Contains('s'))
                        {
                            if (!hasSni) return -1;
                            pos += sniEndPos!.Value - sniPos!.Value;   // from SNI start to SNI end
                        }
                        else
                        {
                            pos += payloadLen;
                        }
                        break;
                    case 'm':
                        if (point.Flags.Contains('s'))
                        {
                            if (!hasSni) return -1;
                            pos += (sniEndPos!.Value - sniPos!.Value) / 2;   // half of the SNI
                        }
                        else
                        {
                            pos += payloadLen / 2;
                        }
                        break;
                }
            }

            return pos;
        }
    }
}

using System.Text;

namespace ProxyMapService.Proxy.Http
{
    public class HttpParser : IDisposable
    {
        private readonly MemoryStream _memoryStream = new();

        private int _searchHeadersStart = 0;
        private bool _readingHeaders = false;

        private int _headerLength = 0;
        private HttpHeaderLinesAndTail? _headerAndTail = null;

        private readonly bool _response;
        private readonly bool _initialReadHeaders;

        public bool ReadingHeaders { get => _readingHeaders; }

        public int HeaderLength { get => _headerLength; }
        public HttpHeaderLinesAndTail? HeaderAndTail { get => _headerAndTail; }

        public HttpParser(bool response, bool readHeaders) {
            _response = response;
            _initialReadHeaders = readHeaders;
            _readingHeaders = readHeaders;
        }

        public void Reset()
        {
            _memoryStream.SetLength(0);
            _memoryStream.Position = 0;
            _searchHeadersStart = 0;
            _readingHeaders = _initialReadHeaders;
            _headerLength = 0;
            _headerAndTail = null;
        }

        public void AppendData(ReadOnlySpan<byte> data, out bool endOfHeaders)
        {
            if (_readingHeaders)
            {
                _memoryStream.Write(data);
                int headersEnd;
                if ((headersEnd = FindHeadersEnd(_memoryStream, _response, ref _searchHeadersStart)) >= 0 || _searchHeadersStart < 0)
                {
                    _readingHeaders = false;
                    _headerLength = headersEnd >= 0 ? headersEnd + 4 : 0;
                    _headerAndTail = GetHeaderLinesAndTail(_memoryStream, _response, headersEnd);
                    endOfHeaders = true;
                    return;
                }
            }
            endOfHeaders = false;
        }

        public async Task CopyHeaderStreamAsync(Stream destination)
        {
            _memoryStream.Position = 0;
            await _memoryStream.CopyToAsync(destination);
        }

        public async Task WriteHeaderStreamAsync(Stream destination, CancellationToken cancellationToken)
        {
            await destination.WriteAsync(_memoryStream.GetBuffer().AsMemory(0, (int)_memoryStream.Length), cancellationToken);
        }

        public void ClearHeaderStream()
        {
            _memoryStream.SetLength(0);
            _memoryStream.Position = 0;
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (disposing)
            {
                _memoryStream.Dispose();
            }
        }

        private static readonly string[] HttpMethods =
        {
            "GET", "POST", "PUT", "DELETE",
            "HEAD", "OPTIONS", "PATCH", "TRACE", 
            "CONNECT"
        };

        public enum HttpMethodEnum
        {
            Get = 0, Post = 1, Put = 2, Delete = 3, 
            Head = 4, Options = 5, Patch = 6, Trace = 7,
            Connect = 8
        }

        private static readonly byte[][] HttpMethodPrefixBytes =
            HttpMethods.Select(m => Encoding.ASCII.GetBytes(m + " ")).ToArray();

        private static readonly string[] HttpMethodPrefixStrings =
            HttpMethods.Select(m => m + " ").ToArray();

        private static readonly string[] HttpVersions =
        {
            "HTTP/1.0",
            "HTTP/1.1"
        };

        public enum HttpVersionEnum
        {
            Http10 = 0,
            Http11 = 1
        }

        private static readonly byte[][] HttpVersionPrefixBytes =
            HttpVersions.Select(m => Encoding.ASCII.GetBytes(m + " ")).ToArray();

        private static readonly string[] HttpVersionPrefixStrings =
            HttpVersions.Select(m => m + " ").ToArray();

        public static bool IsValidHttpMethod(ReadOnlySpan<char> methodSpan)
        {
            return methodSpan switch
            {
                "GET" => true,
                "POST" => true,
                "PUT" => true,
                "DELETE" => true,
                "HEAD" => true,
                "OPTIONS" => true,
                "PATCH" => true,
                "TRACE" => true,
                "CONNECT" => true,
                _ => false
            };
        }

        public static bool IsValidHttpVersion(ReadOnlySpan<char> versionSpan)
        {
            return versionSpan switch
            {
                "HTTP/1.0" => true,
                "HTTP/1.1" => true,
                _ => false
            };
        }

        public static HttpMethodEnum? ParseHttpMethod(ReadOnlySpan<char> methodSpan)
        {
            return methodSpan switch
            {
                "GET" => HttpMethodEnum.Get,
                "POST" => HttpMethodEnum.Post,
                "PUT" => HttpMethodEnum.Put,
                "DELETE" => HttpMethodEnum.Delete,
                "HEAD" => HttpMethodEnum.Head,
                "OPTIONS" => HttpMethodEnum.Options,
                "PATCH" => HttpMethodEnum.Patch,
                "TRACE" => HttpMethodEnum.Trace,
                "CONNECT" => HttpMethodEnum.Connect,
                _ => null
            };
        }

        public static HttpVersionEnum? ParseHttpVersion(ReadOnlySpan<char> versionSpan)
        {
            return versionSpan switch
            {
                "HTTP/1.0" => HttpVersionEnum.Http10,
                "HTTP/1.1" => HttpVersionEnum.Http11,
                _ => null
            };
        }

        public static int StartsWithHttpMethod(ReadOnlySpan<byte> lineSpan, bool partially)
        {
            foreach (var method in HttpMethodPrefixBytes)
            {
                var methodSpan = method.AsSpan();
                int compareLength = partially ? Math.Min(lineSpan.Length, methodSpan.Length) : methodSpan.Length;
                if (compareLength <= lineSpan.Length && lineSpan.Slice(0, compareLength)
                        .SequenceEqual(methodSpan.Slice(0, compareLength)))
                {
                    return compareLength;
                }
            }

            return 0;
        }

        public static int StartsWithHttpMethod(ReadOnlySpan<char> lineSpan, bool partially)
        {
            foreach (var method in HttpMethodPrefixStrings)
            {
                var methodSpan = method.AsSpan();
                int compareLength = partially ? Math.Min(lineSpan.Length, methodSpan.Length) : methodSpan.Length;
                if (compareLength <= lineSpan.Length && lineSpan.Slice(0, compareLength)
                        .SequenceEqual(methodSpan.Slice(0, compareLength)))
                {
                    return compareLength;
                }
            }
            return 0;
        }

        public static int StartsWithHttpVersion(ReadOnlySpan<byte> lineSpan, bool partially)
        {
            foreach (var version in HttpVersionPrefixBytes)
            {
                var versionSpan = version.AsSpan();
                int compareLength = partially ? Math.Min(lineSpan.Length, versionSpan.Length) : versionSpan.Length;
                if (compareLength <= lineSpan.Length && lineSpan.Slice(0, compareLength)
                        .SequenceEqual(versionSpan.Slice(0, compareLength)))
                {
                    return compareLength;
                }
            }

            return 0;
        }

        public static int StartsWithHttpVersion(ReadOnlySpan<char> lineSpan, bool partially)
        {
            foreach (var version in HttpVersionPrefixStrings)
            {
                var versionSpan = version.AsSpan();
                int compareLength = partially ? Math.Min(lineSpan.Length, versionSpan.Length) : versionSpan.Length;
                if (compareLength <= lineSpan.Length && lineSpan.Slice(0, compareLength)
                        .SequenceEqual(versionSpan.Slice(0, compareLength)))
                {
                    return compareLength;
                }
            }
            return 0;
        }

        public static int FindHeadersEnd(MemoryStream ms, bool response, ref int searchStart)
        {
            var span = ms.GetBuffer().AsSpan(0, (int)ms.Length);

            if (searchStart < 0)
                return -1; // Searching was terminated

            if (response)
            {
                // Validate the beginning of the HTTP response
                if (StartsWithHttpVersion(span, true) <= 0)
                {
                    // ATTENTION!!! Terminate searching
                    searchStart = -1;
                    return -1;
                }
            }
            else
            {
                // Validate the beginning of the HTTP request
                if (StartsWithHttpMethod(span, true) <= 0)
                {
                    // ATTENTION!!! Terminate searching
                    searchStart = -1;
                    return -1;
                }
            }

            int index = span.Slice(searchStart).IndexOf("\r\n\r\n"u8);
            if (index >= 0)
            {
                index += searchStart;
            }
            else
            {
                searchStart = Math.Max(0, span.Length - 3);
            }

            return index;
        }

        public static int FindRequestHeadersEnd(MemoryStream ms, ref int searchStart)
        {
            return FindHeadersEnd(ms, false, ref searchStart);
        }

        public static int FindResponseHeadersEnd(MemoryStream ms, ref int searchStart)
        {
            return FindHeadersEnd(ms, true, ref searchStart);
        }

        public static byte[]? GetHeaderBytes(MemoryStream ms, bool response, int headersEnd)
        {
            var span = ms.GetBuffer().AsSpan(0, (int)ms.Length);

            // Validate the end of the HTTP headers
            if (headersEnd < 0)
                return null;
            if (span.Length < headersEnd + 4)
                return null;
            if (!span.Slice(headersEnd, 4).SequenceEqual("\r\n\r\n"u8))
                return null;

            if (response)
            {
                // Validate the beginning of the HTTP response
                if (StartsWithHttpVersion(span, false) <= 0)
                    return null;
            }
            else
            {
                // Validate the beginning of the HTTP request
                if (StartsWithHttpMethod(span, false) <= 0)
                    return null;
            }

            int headerSectionLength = headersEnd + 4; // include \r\n\r\n

            return span.Slice(0, headerSectionLength).ToArray();
        }

        public static byte[]? GetRequestHeaderBytes(MemoryStream ms, int headersEnd)
        {
            return GetHeaderBytes(ms, false, headersEnd);
        }

        public static byte[]? GetResponseHeaderBytes(MemoryStream ms, int headersEnd)
        {
            return GetHeaderBytes(ms, true, headersEnd);
        }

        public static HttpHeaderLinesAndTail? GetHeaderLinesAndTail(MemoryStream ms, bool response, int headersEnd)
        {
            var span = ms.GetBuffer().AsSpan(0, (int)ms.Length);

            // Validate the end of the HTTP headers
            if (headersEnd < 0)
                return null;
            if (span.Length < headersEnd + 4)
                return null;
            if (!span.Slice(headersEnd, 4).SequenceEqual("\r\n\r\n"u8))
                return null;

            if (response)
            {
                // Validate the beginning of the HTTP response
                if (StartsWithHttpVersion(span, false) <= 0)
                    return null;
            }
            else
            {
                // Validate the beginning of the HTTP request
                if (StartsWithHttpMethod(span, false) <= 0)
                    return null;
            }

            int headerSectionLength = headersEnd + 4; // include \r\n\r\n

            var headerSpan = span.Slice(0, headerSectionLength);
            var tail = span.Slice(headerSectionLength);

            string header = Encoding.ASCII.GetString(headerSpan);

            var lines = header.Split(["\r\n"], StringSplitOptions.None);

            byte[]? tailBytes = tail.Length > 0 ? tail.ToArray() : null;

            return new HttpHeaderLinesAndTail
            {
                HeaderLines = lines,
                TailBytes = tailBytes,
            };
        }

        public static HttpHeaderLinesAndTail? GetRequestHeaderLinesAndTail(MemoryStream ms, int headersEnd)
        {
            return GetHeaderLinesAndTail(ms, false, headersEnd);
        }

        public static HttpHeaderLinesAndTail? GetResponseHeaderLinesAndTail(MemoryStream ms, int headersEnd)
        {
            return GetHeaderLinesAndTail(ms, true, headersEnd);
        }
    }
}

using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using ProxyMapService.WebLogging.Dtos;
using System.IO.Compression;
using System.Text;
using ZstdSharp;

namespace ProxyMapService.WebLogging
{
    public static class HttpBodyParser
    {
        public static HttpBodyDto ParseBody(string id, bool completed, long bodyLength, string? contentType, string? contentEncoding, ReadOnlySpan<byte> bodySpan)
        {
            bodySpan = TryDecompress(bodySpan, contentEncoding, out var decompressedBytes);

            var kind = GetContentKind(contentType, HttpBodyContentKind.Binary);

            var dto = new HttpBodyDto
            {
                Id = id,
                Completed = completed,
                Length = decompressedBytes?.Length ?? bodyLength,
                CompressedLength = decompressedBytes != null ? bodyLength : null,
                ContentType = contentType,
                ContentKind = kind,
            };

            switch (kind)
            {
                case HttpBodyContentKind.Json:
                case HttpBodyContentKind.Xml:
                case HttpBodyContentKind.Html:
                case HttpBodyContentKind.Text:
                case HttpBodyContentKind.FormUrlEncoded:
                case HttpBodyContentKind.Javascript:
                case HttpBodyContentKind.Typescript:
                    dto.Content = Encoding.UTF8.GetString(bodySpan);
                    break;

                case HttpBodyContentKind.MultipartFormData:
                    try
                    {
                        ParseMultipart(dto, bodySpan);
                    }
                    catch
                    {
                        dto.ContentKind = HttpBodyContentKind.Text;
                        dto.Content = Encoding.UTF8.GetString(bodySpan);
                    }
                    break;

                case HttpBodyContentKind.Image:
                case HttpBodyContentKind.Binary:
                default:
                    dto.BinaryContentBase64 = Convert.ToBase64String(bodySpan);
                    break;
            }

            return dto;
        }

        private static void ParseMultipart(HttpMultipartBodyDto dto, ReadOnlySpan<byte> multipartBytes)
        {
            var mediaType = MediaTypeHeaderValue.Parse(dto.ContentType);
            var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;

            if (string.IsNullOrWhiteSpace(boundary))
            {
                throw new InvalidOperationException("Multipart boundary is missing.");
            }

            using var stream = new MemoryStream();

            stream.Write(multipartBytes);
            stream.Position = 0;
            
            var reader = new MultipartReader(boundary, stream);

            MultipartSection? section;

            while ((section = reader.ReadNextSectionAsync().GetAwaiter().GetResult()) != null)
            {
                var disposition = ContentDispositionHeaderValue.Parse(section.ContentDisposition);

                using var ms = new MemoryStream();
                section.Body.CopyTo(ms);

                int streamLength = (int)ms.Length;
                ReadOnlySpan<byte> bytesSpan = ms.GetBuffer().AsSpan(0, streamLength);

                var kind = GetContentKind(section.ContentType, HttpBodyContentKind.Text);

                var part = new HttpMultipartPartDto
                {
                    Name = HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? "",
                    FileName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar).Value
                            ?? HeaderUtilities.RemoveQuotes(disposition.FileName).Value,
                    ContentType = section.ContentType,
                    Length = streamLength,
                    ContentKind = kind,
                };

                switch (kind)
                {
                    case HttpBodyContentKind.Json:
                    case HttpBodyContentKind.Xml:
                    case HttpBodyContentKind.Html:
                    case HttpBodyContentKind.Text:
                    case HttpBodyContentKind.FormUrlEncoded:
                    case HttpBodyContentKind.Javascript:
                    case HttpBodyContentKind.Typescript:
                        part.Content = Encoding.UTF8.GetString(bytesSpan);
                        break;

                    case HttpBodyContentKind.MultipartFormData:
                        try
                        {
                            ParseMultipart(part, bytesSpan);
                        }
                        catch
                        {
                            part.ContentKind = HttpBodyContentKind.Text;
                            part.Content = Encoding.UTF8.GetString(bytesSpan);
                        }
                        break;

                    case HttpBodyContentKind.Image:
                    case HttpBodyContentKind.Binary:
                    default:
                        part.BinaryContentBase64 = Convert.ToBase64String(bytesSpan);
                        break;
                }

                dto.Parts ??= [];
                dto.Parts.Add(part);
            }
        }
        
        private static ReadOnlySpan<byte> TryDecompress(ReadOnlySpan<byte> data, string? contentEncoding, out byte[]? decompressedBytes)
        {
            try
            {
                if (data.Length == 0 || string.IsNullOrWhiteSpace(contentEncoding))
                {
                    decompressedBytes = null;
                    return data;
                }

                var encodings = contentEncoding
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                decompressedBytes = null;

                for (int i = encodings.Length - 1; i >= 0; i--)
                {
                    data = Decompress(data, encodings[i], out decompressedBytes);
                }

                return data;
            }
            catch
            {
                decompressedBytes = null;
                return data;
            }
        }

        private static ReadOnlySpan<byte> Decompress(ReadOnlySpan<byte> data, string encoding, out byte[]? decompressedBytes)
        {
            encoding = encoding.ToLowerInvariant();

            decompressedBytes = null;

            return encoding switch
            {
                "gzip" => DecompressStream(data, s => new GZipStream(s, CompressionMode.Decompress), out decompressedBytes),
                "deflate" => DecompressStream(data, s => new DeflateStream(s, CompressionMode.Decompress), out decompressedBytes),
                "br" => DecompressStream(data, s => new BrotliStream(s, CompressionMode.Decompress), out decompressedBytes),
                "zstd" => DecompressZstd(data, out decompressedBytes),
                "identity" => data,
                _ => data
            };
        }

        private static ReadOnlySpan<byte> DecompressStream(ReadOnlySpan<byte> data, Func<Stream, Stream> createStream, out byte[]? decompressedBytes)
        {
            using var input = new MemoryStream();

            input.Write(data);
            input.Position = 0;

            using var stream = createStream(input);

            using var output = new MemoryStream();

            stream.CopyTo(output);

            decompressedBytes = output.ToArray();

            return decompressedBytes;
        }

        private static ReadOnlySpan<byte> DecompressZstd(ReadOnlySpan<byte> data, out byte[]? decompressedBytes)
        {
            ulong size = Decompressor.GetDecompressedSize(data);

            if (size == 0 || size > int.MaxValue)
                throw new InvalidDataException();

            decompressedBytes = new byte[(int)size];

            var decompressor = new Decompressor();
            decompressor.Unwrap(data, decompressedBytes);

            return decompressedBytes;
        }

        private static HttpBodyContentKind GetContentKind(string? contentType, HttpBodyContentKind emptyContentTypeKind)
        {
            if (string.IsNullOrWhiteSpace(contentType))
            {
                return emptyContentTypeKind;
            }

            var mediaType = contentType
                .Split(';', 2)[0]
                .Trim()
                .ToLowerInvariant();

            if (mediaType.Contains("json"))
            {
                return HttpBodyContentKind.Json;
            }

            if (mediaType == "application/x-www-form-urlencoded")
            {
                return HttpBodyContentKind.FormUrlEncoded;
            }

            if (mediaType.StartsWith("multipart/form-data"))
            {
                return HttpBodyContentKind.MultipartFormData;
            }

            if (mediaType.StartsWith("image/"))
            {
                return HttpBodyContentKind.Image;
            }

            if (mediaType is "application/xml" or "text/xml")
            {
                return HttpBodyContentKind.Xml;
            }

            if (mediaType.EndsWith("+xml"))
            {
                return HttpBodyContentKind.Xml;
            }

            if (mediaType is "application/javascript" or "text/javascript" or "application/x-javascript")
            {
                return HttpBodyContentKind.Javascript;
            }

            if (mediaType is "application/typescript" or "text/typescript")
            {
                return HttpBodyContentKind.Typescript;
            }

            if (mediaType == "text/html")
            {
                return HttpBodyContentKind.Html;
            }

            if (mediaType.StartsWith("text/"))
            {
                return HttpBodyContentKind.Text;
            }

            if (mediaType == "application/octet-stream")
            {
                return HttpBodyContentKind.Binary;
            }

            return HttpBodyContentKind.Binary;
        }
    }
}

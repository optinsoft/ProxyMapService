using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using ProxyMapService.WebLogging.Dtos;
using System.Text;

namespace ProxyMapService.WebLogging
{
    public static class HttpBodyParser
    {
        public static HttpBodyDto ParseBody(string id, long bodyLength, string? contentType, byte[] bodyBytes)
        {
            var kind = GetContentKind(contentType, HttpBodyContentKind.Binary);

            var dto = new HttpBodyDto
            {
                Id = id,
                Length = bodyLength,
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
                    dto.Content = Encoding.UTF8.GetString(bodyBytes);
                    break;

                case HttpBodyContentKind.MultipartFormData:
                    ParseMultipart(dto, bodyBytes);
                    break;

                case HttpBodyContentKind.Image:
                case HttpBodyContentKind.Binary:
                default:
                    dto.BinaryContentBase64 = Convert.ToBase64String(bodyBytes);
                    break;
            }

            return dto;
        }

        private static void ParseMultipart(HttpMultipartBodyDto dto, byte[] bodyBytes)
        {
            var mediaType = MediaTypeHeaderValue.Parse(dto.ContentType);
            var boundary = HeaderUtilities.RemoveQuotes(mediaType.Boundary).Value;

            if (string.IsNullOrWhiteSpace(boundary))
            {
                throw new InvalidOperationException("Multipart boundary is missing.");
            }

            using var stream = new MemoryStream(bodyBytes);
            
            var reader = new MultipartReader(boundary, stream);

            MultipartSection? section;

            while ((section = reader.ReadNextSectionAsync().GetAwaiter().GetResult()) != null)
            {
                var disposition = ContentDispositionHeaderValue.Parse(section.ContentDisposition);

                using var ms = new MemoryStream();
                section.Body.CopyTo(ms);

                var bytes = ms.ToArray();

                var kind = GetContentKind(section.ContentType, HttpBodyContentKind.Text);

                var part = new HttpMultipartPartDto
                {
                    Name = HeaderUtilities.RemoveQuotes(disposition.Name).Value ?? "",
                    FileName = HeaderUtilities.RemoveQuotes(disposition.FileNameStar).Value
                            ?? HeaderUtilities.RemoveQuotes(disposition.FileName).Value,
                    ContentType = section.ContentType,
                    Length = bytes.Length,
                    ContentKind = kind,
                };

                switch (kind)
                {
                    case HttpBodyContentKind.Json:
                    case HttpBodyContentKind.Xml:
                    case HttpBodyContentKind.Html:
                    case HttpBodyContentKind.Text:
                    case HttpBodyContentKind.FormUrlEncoded:
                        part.Content = Encoding.UTF8.GetString(bytes);
                        break;

                    case HttpBodyContentKind.MultipartFormData:
                        ParseMultipart(part, bytes);
                        break;

                    case HttpBodyContentKind.Image:
                    case HttpBodyContentKind.Binary:
                    default:
                        part.BinaryContentBase64 = Convert.ToBase64String(bytes);
                        break;
                }

                dto.Parts ??= [];
                dto.Parts.Add(part);
            }
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

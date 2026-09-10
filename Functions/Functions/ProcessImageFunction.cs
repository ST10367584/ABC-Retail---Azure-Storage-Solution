using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ABCRetail.Functions.Services;

namespace ABCRetail.Functions.Functions
{
    public class ProcessImageFunction
    {
        private readonly ILogger _logger;
        private readonly IStorageService _storage;

        public ProcessImageFunction(ILoggerFactory loggerFactory, IStorageService storage)
        {
            _logger = loggerFactory.CreateLogger<ProcessImageFunction>();
            _storage = storage;
        }

        [Function("ProcessImage")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("✅ ProcessImage function triggered");

            try
            {
                // Read the body as bytes
                using var memoryStream = new MemoryStream();
                await req.Body.CopyToAsync(memoryStream);
                var bodyBytes = memoryStream.ToArray();

                // Parse the multipart form data manually
                var contentType = req.Headers.GetValues("Content-Type")?.FirstOrDefault() ?? "";
                var boundary = GetBoundary(contentType);

                if (string.IsNullOrEmpty(boundary))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid Content-Type. Expected multipart/form-data with boundary.");
                    return badResponse;
                }

                // Parse multipart data
                var (fileBytes, fileName, containerName) = ParseMultipart(bodyBytes, boundary);

                if (fileBytes == null || fileBytes.Length == 0 || string.IsNullOrEmpty(fileName))
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("No file uploaded or file is empty");
                    return badResponse;
                }

                containerName = string.IsNullOrEmpty(containerName) ? "productimages" : containerName;
                var blobName = $"{Guid.NewGuid()}{Path.GetExtension(fileName)}";
                var fileContentType = GetContentType(fileName);

                // Upload to Blob Storage
                using var fileStream = new MemoryStream(fileBytes);
                var url = await _storage.UploadBlobAsync(fileStream, containerName, blobName, fileContentType);

                _logger.LogInformation($"✅ Image uploaded: {blobName} ({fileBytes.Length} bytes)");

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Image uploaded successfully: {url}");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        // Extract boundary from Content-Type header
        private static string? GetBoundary(string contentType)
        {
            if (string.IsNullOrEmpty(contentType))
                return null;

            var parts = contentType.Split(';');
            foreach (var part in parts)
            {
                var trimmed = part.Trim();
                if (trimmed.StartsWith("boundary=", StringComparison.OrdinalIgnoreCase))
                {
                    return trimmed.Substring("boundary=".Length).Trim('"');
                }
            }
            return null;
        }

        // Parse multipart form data manually
        private static (byte[]? fileBytes, string? fileName, string? containerName) ParseMultipart(byte[] body, string boundary)
        {
            byte[]? fileBytes = null;
            string? fileName = null;
            string? containerName = null;

            var boundaryBytes = Encoding.UTF8.GetBytes("--" + boundary);
            var endBoundaryBytes = Encoding.UTF8.GetBytes("--" + boundary + "--");

            // Find all boundary positions
            var positions = new List<int>();
            for (int i = 0; i < body.Length - boundaryBytes.Length; i++)
            {
                if (MatchesAt(body, i, boundaryBytes))
                {
                    positions.Add(i);
                }
            }

            // Parse each part between boundaries
            for (int i = 0; i < positions.Count - 1; i++)
            {
                var start = positions[i] + boundaryBytes.Length;
                var end = positions[i + 1];

                // Skip CRLF after boundary
                if (start + 1 < body.Length && body[start] == '\r' && body[start + 1] == '\n')
                    start += 2;

                var partBytes = new byte[end - start];
                Array.Copy(body, start, partBytes, 0, partBytes.Length);

                // Parse headers and content
                var part = ParsePart(partBytes);
                if (part.HasValue)
                {
                    var (headers, content) = part.Value;

                    // Check if this is a file
                    if (headers.ContainsKey("Content-Disposition") &&
                        headers["Content-Disposition"].Contains("filename="))
                    {
                        fileName = ExtractAttribute(headers["Content-Disposition"], "filename");
                        fileBytes = content;
                    }
                    // Check if this is the container name
                    else if (headers.ContainsKey("Content-Disposition") &&
                             headers["Content-Disposition"].Contains("name=\"containerName\""))
                    {
                        containerName = Encoding.UTF8.GetString(content).Trim();
                    }
                }
            }

            return (fileBytes, fileName, containerName);
        }

        // Parse a single part (headers + content)
        private static (Dictionary<string, string> headers, byte[] content)? ParsePart(byte[] partBytes)
        {
            // Find the header/content separator (double CRLF)
            int separatorIndex = -1;
            for (int i = 0; i < partBytes.Length - 3; i++)
            {
                if (partBytes[i] == '\r' && partBytes[i + 1] == '\n' &&
                    partBytes[i + 2] == '\r' && partBytes[i + 3] == '\n')
                {
                    separatorIndex = i;
                    break;
                }
            }

            if (separatorIndex < 0)
                return null;

            // Parse headers
            var headerBytes = new byte[separatorIndex];
            Array.Copy(partBytes, 0, headerBytes, 0, separatorIndex);
            var headerText = Encoding.UTF8.GetString(headerBytes);

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var line in headerText.Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries))
            {
                var colonIndex = line.IndexOf(':');
                if (colonIndex > 0)
                {
                    var key = line.Substring(0, colonIndex).Trim();
                    var value = line.Substring(colonIndex + 1).Trim();
                    headers[key] = value;
                }
            }

            // Extract content (skip the double CRLF, and trailing CRLF)
            var contentStart = separatorIndex + 4;
            var contentEnd = partBytes.Length;

            // Remove trailing CRLF
            if (contentEnd >= 2 && partBytes[contentEnd - 2] == '\r' && partBytes[contentEnd - 1] == '\n')
                contentEnd -= 2;

            var content = new byte[contentEnd - contentStart];
            Array.Copy(partBytes, contentStart, content, 0, content.Length);

            return (headers, content);
        }

        // Extract attribute value from header (e.g., filename="test.jpg")
        private static string? ExtractAttribute(string headerValue, string attributeName)
        {
            var searchKey = attributeName + "=";
            var index = headerValue.IndexOf(searchKey, StringComparison.OrdinalIgnoreCase);
            if (index < 0)
                return null;

            var valueStart = index + searchKey.Length;
            if (valueStart >= headerValue.Length)
                return null;

            // Check if quoted
            if (headerValue[valueStart] == '"')
            {
                var endQuote = headerValue.IndexOf('"', valueStart + 1);
                if (endQuote > valueStart)
                {
                    return headerValue.Substring(valueStart + 1, endQuote - valueStart - 1);
                }
            }
            else
            {
                var end = headerValue.IndexOf(';', valueStart);
                if (end < 0) end = headerValue.Length;
                return headerValue.Substring(valueStart, end - valueStart).Trim();
            }

            return null;
        }

        // Check if bytes match at position
        private static bool MatchesAt(byte[] source, int position, byte[] pattern)
        {
            if (position + pattern.Length > source.Length)
                return false;

            for (int i = 0; i < pattern.Length; i++)
            {
                if (source[position + i] != pattern[i])
                    return false;
            }
            return true;
        }

        // Get content type from file extension
        private static string GetContentType(string fileName)
        {
            var ext = Path.GetExtension(fileName).ToLowerInvariant();
            return ext switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                ".svg" => "image/svg+xml",
                _ => "application/octet-stream"
            };
        }
    }
}
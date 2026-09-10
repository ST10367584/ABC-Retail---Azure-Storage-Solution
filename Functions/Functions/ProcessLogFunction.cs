using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ABCRetail.Functions.Models;
using ABCRetail.Functions.Services;
using System.Text.Json;

namespace ABCRetail.Functions.Functions
{
    public class ProcessLogFunction
    {
        private readonly ILogger _logger;
        private readonly IStorageService _storage;

        public ProcessLogFunction(ILoggerFactory loggerFactory, IStorageService storage)
        {
            _logger = loggerFactory.CreateLogger<ProcessLogFunction>();
            _storage = storage;
        }

        [Function("ProcessLog")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("✅ ProcessLog function triggered");

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var logEntry = JsonSerializer.Deserialize<LogEntry>(body);

                if (logEntry == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid log data");
                    return badResponse;
                }

                // Set defaults
                logEntry.LogId = logEntry.LogId ?? Guid.NewGuid().ToString();
                logEntry.Timestamp = logEntry.Timestamp == DateTime.MinValue ? DateTime.UtcNow : logEntry.Timestamp;
                logEntry.Application = logEntry.Application ?? "ABC Retail";

                // Write to File Storage
                var fileName = $"{logEntry.LogId}.log";
                await _storage.WriteFileAsync("logs", "application-logs", fileName, logEntry.ToLogString());
                _logger.LogInformation($"✅ Log '{logEntry.LogId}' saved to file storage");

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Log '{logEntry.LogId}' saved successfully");
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"❌ Error: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }
    }
}
using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ABCRetail.Functions.Models;
using ABCRetail.Functions.Services;
using System.Text.Json;

namespace ABCRetail.Functions.Functions
{
    public class ProcessOrderFunction
    {
        private readonly ILogger _logger;
        private readonly IStorageService _storage;

        public ProcessOrderFunction(ILoggerFactory loggerFactory, IStorageService storage)
        {
            _logger = loggerFactory.CreateLogger<ProcessOrderFunction>();
            _storage = storage;
        }

        [Function("ProcessOrder")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("✅ ProcessOrder function triggered");

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var order = JsonSerializer.Deserialize<OrderMessage>(body);

                if (order == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid order data");
                    return badResponse;
                }

                // Generate Order ID if not provided
                order.OrderId = order.OrderId ?? Guid.NewGuid().ToString();
                order.OrderDate = order.OrderDate == DateTime.MinValue ? DateTime.UtcNow : order.OrderDate;
                order.Status = "Queued";

                // Send to Queue
                var queueName = "orderprocessing";
                var message = order.ToJson();
                await _storage.SendQueueMessageAsync(queueName, message);
                _logger.LogInformation($"✅ Order '{order.OrderId}' queued successfully");

                // Log the order
                var log = new LogEntry
                {
                    Application = "ABC Retail",
                    Level = "Information",
                    Message = $"Order '{order.OrderId}' queued for customer '{order.CustomerName}'",
                    Source = "ProcessOrderFunction"
                };
                await _storage.WriteFileAsync("logs", "application-logs", $"{log.LogId}.log", log.ToLogString());

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Order '{order.OrderId}' queued successfully");
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
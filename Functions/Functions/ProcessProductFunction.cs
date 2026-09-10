using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using ABCRetail.Functions.Models;
using ABCRetail.Functions.Services;
using System.Text.Json;

namespace ABCRetail.Functions.Functions
{
    public class ProcessProductFunction
    {
        private readonly ILogger _logger;
        private readonly IStorageService _storage;

        public ProcessProductFunction(ILoggerFactory loggerFactory, IStorageService storage)
        {
            _logger = loggerFactory.CreateLogger<ProcessProductFunction>();
            _storage = storage;
        }

        [Function("ProcessProduct")]
        public async Task<HttpResponseData> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequestData req)
        {
            _logger.LogInformation("✅ ProcessProduct function triggered");

            try
            {
                var body = await new StreamReader(req.Body).ReadToEndAsync();
                var product = JsonSerializer.Deserialize<Product>(body);

                if (product == null)
                {
                    var badResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badResponse.WriteStringAsync("Invalid product data");
                    return badResponse;
                }

                // Set required fields
                product.ProductId = product.ProductId ?? Guid.NewGuid().ToString();
                product.RowKey = product.ProductId;
                product.PartitionKey = "Product";
                product.DateAdded = product.DateAdded ?? DateTime.UtcNow;

                // Store in Table Storage
                await _storage.AddEntityAsync(product, "Products");
                _logger.LogInformation($"✅ Product '{product.Name}' saved with price {product.Price}");

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync($"Product '{product.Name}' processed successfully with price {product.Price}");
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
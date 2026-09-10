using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.AzureStorage.Controllers
{
    [Authorize]
    public class OrdersController : Controller
    {
        private readonly IQueueStorageService _queueService;
        private readonly ITableStorageService _tableService;
        private readonly IAppLogger _appLogger;

        public OrdersController(
            IQueueStorageService queueService,
            ITableStorageService tableService,
            IAppLogger appLogger)
        {
            _queueService = queueService;
            _tableService = tableService;
            _appLogger = appLogger;
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Index()
        {
            var messages = await _queueService.PeekOrderMessagesAsync(64);
            var queueLength = await _queueService.GetQueueLengthAsync();
            ViewBag.QueueLength = queueLength;
            return View(messages);
        }

        public async Task<IActionResult> Create()
        {
            var customers = await _tableService.GetAllCustomersAsync();
            var products = await _tableService.GetAllProductsAsync();
            ViewBag.Customers = customers;
            ViewBag.Products = products;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(OrderMessage order)
        {
            if (ModelState.IsValid)
            {
                order.OrderId = Guid.NewGuid().ToString();
                order.OrderDate = DateTime.UtcNow;
                order.Status = "Processing";

                await _queueService.SendOrderMessageAsync(order);

                await _appLogger.LogAsync("Information", "OrdersController",
                    $"Order created: {order.OrderId} for {order.CustomerName} - R{order.TotalAmount.ToString("F2")} ({order.Items.Count} items)",
                    User.Identity?.Name);

                TempData["SuccessMessage"] = $"Order created successfully! Total: R{order.TotalAmount.ToString("F2")}";
                return RedirectToAction(nameof(Index));
            }

            var customers = await _tableService.GetAllCustomersAsync();
            var products = await _tableService.GetAllProductsAsync();
            ViewBag.Customers = customers;
            ViewBag.Products = products;
            return View(order);
        }

        [HttpPost]
        [Authorize(Roles = "Admin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessNext()
        {
            var order = await _queueService.ReceiveOrderMessageAsync();
            if (order != null)
            {
                TempData["ProcessedOrder"] = $"Order {order.OrderId} processed for customer {order.CustomerName}";

                await _appLogger.LogAsync("Information", "OrdersController",
                    $"Order processed from queue: {order.OrderId} - {order.CustomerName}",
                    User.Identity?.Name);
            }
            else
            {
                TempData["ProcessedOrder"] = "No orders in queue to process.";

                await _appLogger.LogAsync("Warning", "OrdersController",
                    "Attempted to process order but queue was empty",
                    User.Identity?.Name);
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
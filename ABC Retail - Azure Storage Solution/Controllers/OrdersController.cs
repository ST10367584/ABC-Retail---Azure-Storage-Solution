using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.AzureStorage.Controllers
{
    public class OrdersController : Controller
    {
        private readonly IQueueStorageService _queueService;
        private readonly ITableStorageService _tableService;

        public OrdersController(IQueueStorageService queueService, ITableStorageService tableService)
        {
            _queueService = queueService;
            _tableService = tableService;
        }

        public async Task<IActionResult> Index()
        {
            var messages = await _queueService.PeekOrderMessagesAsync(20);
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
                return RedirectToAction(nameof(Index));
            }

            var customers = await _tableService.GetAllCustomersAsync();
            var products = await _tableService.GetAllProductsAsync();
            ViewBag.Customers = customers;
            ViewBag.Products = products;
            return View(order);
        }

        [HttpPost]
        public async Task<IActionResult> ProcessNext()
        {
            var order = await _queueService.ReceiveOrderMessageAsync();
            if (order != null)
            {
                // In a real app, you would process the order here
                // For demo, we just show it and delete from queue
                TempData["ProcessedOrder"] = $"Order {order.OrderId} processed for customer {order.CustomerName}";
            }
            else
            {
                TempData["ProcessedOrder"] = "No orders in queue to process.";
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
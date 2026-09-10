using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ABCRetail.AzureStorage.Controllers
{
    [Authorize]
    public class MyAccountController : Controller
    {
        private readonly ITableStorageService _tableService;
        private readonly IQueueStorageService _queueService;
        private readonly IAppLogger _appLogger;

        public MyAccountController(
            ITableStorageService tableService,
            IQueueStorageService queueService,
            IAppLogger appLogger)
        {
            _tableService = tableService;
            _queueService = queueService;
            _appLogger = appLogger;
        }

        private string GetCustomerId()
        {
            return User.FindFirst("CustomerId")?.Value ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";
        }

        public async Task<IActionResult> Index()
        {
            var customerId = GetCustomerId();
            var customer = await _tableService.GetCustomerAsync(customerId);

            if (customer == null) return NotFound();

            var allOrders = await _queueService.PeekOrderMessagesAsync(64);
            var myOrders = allOrders.Where(o => o.CustomerId == customerId).ToList();

            ViewBag.MyOrders = myOrders;
            ViewBag.TotalSpent = myOrders.Sum(o => o.TotalAmount);

            return View(customer);
        }

        public async Task<IActionResult> OrderHistory()
        {
            var customerId = GetCustomerId();
            var allOrders = await _queueService.PeekOrderMessagesAsync(64);
            var myOrders = allOrders.Where(o => o.CustomerId == customerId).ToList();
            return View(myOrders);
        }

        public IActionResult Cart()
        {
            var cart = HttpContext.Session.GetString("Cart");
            var items = string.IsNullOrEmpty(cart)
                ? new List<OrderItem>()
                : System.Text.Json.JsonSerializer.Deserialize<List<OrderItem>>(cart) ?? new List<OrderItem>();

            ViewBag.Total = items.Sum(i => i.Subtotal);
            return View(items);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddToCart(string productId, string productName, decimal price, int quantity = 1)
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            var cart = string.IsNullOrEmpty(cartJson)
                ? new List<OrderItem>()
                : System.Text.Json.JsonSerializer.Deserialize<List<OrderItem>>(cartJson) ?? new List<OrderItem>();

            var existing = cart.FirstOrDefault(i => i.ProductId == productId);
            if (existing != null)
            {
                existing.Quantity += quantity;
            }
            else
            {
                cart.Add(new OrderItem
                {
                    ProductId = productId,
                    ProductName = productName,
                    Quantity = quantity,
                    UnitPrice = price
                });
            }

            HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(cart));

            await _appLogger.LogAsync("Information", "MyAccountController",
                $"Added to cart: {productName} x{quantity} - R{price.ToString("F2")}",
                User.Identity?.Name);

            TempData["SuccessMessage"] = $"{productName} added to cart!";
            return RedirectToAction("Cart");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RemoveFromCart(string productId)
        {
            var cartJson = HttpContext.Session.GetString("Cart");
            if (!string.IsNullOrEmpty(cartJson))
            {
                var cart = System.Text.Json.JsonSerializer.Deserialize<List<OrderItem>>(cartJson) ?? new List<OrderItem>();
                var removed = cart.FirstOrDefault(i => i.ProductId == productId);
                cart.RemoveAll(i => i.ProductId == productId);
                HttpContext.Session.SetString("Cart", System.Text.Json.JsonSerializer.Serialize(cart));

                if (removed != null)
                {
                    await _appLogger.LogAsync("Information", "MyAccountController",
                        $"Removed from cart: {removed.ProductName}",
                        User.Identity?.Name);
                }
            }
            return RedirectToAction("Cart");
        }

        public async Task<IActionResult> Profile()
        {
            var customerId = GetCustomerId();
            var customer = await _tableService.GetCustomerAsync(customerId);
            return View(customer);
        }
    }
}
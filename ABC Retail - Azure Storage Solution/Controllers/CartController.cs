using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace ABCRetail.AzureStorage.Controllers
{
    [Authorize]
    public class CartController : Controller
    {
        private readonly ICartService _cart;
        private readonly ITableStorageService _tableService;
        private readonly IQueueStorageService _queueService;
        private readonly IAppLogger _appLogger;

        public CartController(
            ICartService cart,
            ITableStorageService tableService,
            IQueueStorageService queueService,
            IAppLogger appLogger)
        {
            _cart = cart;
            _tableService = tableService;
            _queueService = queueService;
            _appLogger = appLogger;
        }

        // GET: /Cart
        public IActionResult Index()
        {
            var cart = _cart.GetCart();
            return View(cart);
        }

        // POST: /Cart/Add
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(string productId, string productName, double price, int quantity = 1, string? imageUrl = null)
        {
            if (string.IsNullOrEmpty(productId))
            {
                TempData["ErrorMessage"] = "Invalid product.";
                return RedirectToAction("Index", "Products");
            }

            _cart.AddItem(new CartItem
            {
                ProductId = productId,
                ProductName = productName,
                UnitPrice = price,
                Quantity = quantity,
                ImageUrl = imageUrl
            });

            await _appLogger.LogAsync("Information", "Cart",
                $"Added to cart: {productName} x{quantity} - R{price:F2}",
                User.Identity?.Name);

            TempData["SuccessMessage"] = $"{productName} added to cart!";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/BuyNow - Add and go straight to checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BuyNow(string productId, string productName, double price, int quantity = 1, string? imageUrl = null)
        {
            _cart.ClearCart();
            _cart.AddItem(new CartItem
            {
                ProductId = productId,
                ProductName = productName,
                UnitPrice = price,
                Quantity = quantity,
                ImageUrl = imageUrl
            });

            await _appLogger.LogAsync("Information", "Cart",
                $"Buy Now: {productName} x{quantity} - R{price:F2}",
                User.Identity?.Name);

            return RedirectToAction(nameof(Checkout));
        }

        // POST: /Cart/Remove
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Remove(string productId)
        {
            var cart = _cart.GetCart();
            var item = cart.Items.FirstOrDefault(i => i.ProductId == productId);
            _cart.RemoveItem(productId);

            if (item != null)
            {
                await _appLogger.LogAsync("Information", "Cart",
                    $"Removed from cart: {item.ProductName}",
                    User.Identity?.Name);
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/UpdateQuantity
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult UpdateQuantity(string productId, int quantity)
        {
            _cart.UpdateQuantity(productId, quantity);
            return RedirectToAction(nameof(Index));
        }

        // POST: /Cart/Clear
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Clear()
        {
            _cart.ClearCart();
            TempData["SuccessMessage"] = "Cart cleared.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Cart/Checkout
        public async Task<IActionResult> Checkout()
        {
            var cart = _cart.GetCart();
            if (!cart.Items.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction(nameof(Index));
            }

            // ✅ AUTO-FILL from logged-in customer
            var model = new CheckoutModel();
            var userId = User.FindFirst("CustomerId")?.Value
                      ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (!string.IsNullOrEmpty(userId))
            {
                var customer = await _tableService.GetCustomerAsync(userId);
                if (customer != null)
                {
                    model.FullName = $"{customer.FirstName} {customer.LastName}".Trim();
                    model.Email = customer.Email ?? "";
                    model.Phone = customer.Phone ?? "";
                    model.Address = customer.Address ?? "";
                    model.City = customer.City ?? "";
                    model.PostalCode = customer.PostalCode ?? "";
                }
            }

            ViewBag.Cart = cart;
            return View(model);
        }

        // POST: /Cart/Checkout
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutModel model)
        {
            var cart = _cart.GetCart();
            if (!cart.Items.Any())
            {
                TempData["ErrorMessage"] = "Your cart is empty.";
                return RedirectToAction(nameof(Index));
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Cart = cart;
                return View(model);
            }

            try
            {
                var userId = User.FindFirst("CustomerId")?.Value
                          ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? "";

                var fullAddress = $"{model.Address}, {model.City}, {model.PostalCode}";
                if (!string.IsNullOrEmpty(model.Notes))
                    fullAddress += $" ({model.Notes})";

                var order = new OrderMessage
                {
                    OrderId = Guid.NewGuid().ToString(),
                    CustomerId = userId,
                    CustomerName = model.FullName,
                    ShippingAddress = fullAddress,
                    Items = cart.Items.Select(i => new OrderItem
                    {
                        ProductId = i.ProductId,
                        ProductName = i.ProductName,
                        Quantity = i.Quantity,
                        UnitPrice = (decimal)i.UnitPrice
                    }).ToList(),
                    TotalAmount = (decimal)cart.Total,
                    OrderDate = DateTime.UtcNow,
                    Status = "Placed"
                };

                await _queueService.SendOrderMessageAsync(order);

                await _appLogger.LogAsync("Information", "Checkout",
                    $"ORDER PLACED: {order.OrderId} | {model.FullName} | R{order.TotalAmount:F2} | {cart.TotalItems} items",
                    User.Identity?.Name);

                _cart.ClearCart();

                TempData["OrderId"] = order.OrderId;
                TempData["OrderTotal"] = order.TotalAmount.ToString("F2");

                return RedirectToAction(nameof(Success));
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error", "Checkout",
                    $"Checkout failed: {ex.Message}",
                    User.Identity?.Name);
                ModelState.AddModelError("", $"Checkout failed: {ex.Message}");
                ViewBag.Cart = cart;
                return View(model);
            }
        }

        // GET: /Cart/Success
        public IActionResult Success()
        {
            if (TempData["OrderId"] == null)
                return RedirectToAction("Index", "Products");

            ViewBag.OrderId = TempData["OrderId"];
            ViewBag.OrderTotal = TempData["OrderTotal"];
            return View();
        }
    }
}
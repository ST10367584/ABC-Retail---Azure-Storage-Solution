using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.AzureStorage.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly ITableStorageService _tableService;
        private readonly IQueueStorageService _queueService;
        private readonly IAppLogger _appLogger;

        public AdminController(
            ITableStorageService tableService,
            IQueueStorageService queueService,
            IAppLogger appLogger)
        {
            _tableService = tableService;
            _queueService = queueService;
            _appLogger = appLogger;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _tableService.GetAllCustomersAsync();
            var products = await _tableService.GetAllProductsAsync();
            var orders = await _queueService.PeekOrderMessagesAsync(64);
            var queueLength = await _queueService.GetQueueLengthAsync();

            ViewBag.TotalCustomers = customers.Count;
            ViewBag.TotalProducts = products.Count;
            ViewBag.TotalOrders = orders.Count;
            ViewBag.QueueLength = queueLength;
            ViewBag.RecentOrders = orders.Take(5).ToList();

            return View();
        }

        public async Task<IActionResult> Customers()
        {
            var customers = await _tableService.GetAllCustomersAsync();
            return View(customers);
        }

        public async Task<IActionResult> Orders()
        {
            var orders = await _queueService.PeekOrderMessagesAsync(64);
            return View(orders);
        }
    }
}
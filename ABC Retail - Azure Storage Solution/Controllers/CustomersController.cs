using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.AzureStorage.Controllers
{
    public class CustomersController : Controller
    {
        private readonly ITableStorageService _tableService;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(ITableStorageService tableService, ILogger<CustomersController> logger)
        {
            _tableService = tableService;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _tableService.GetAllCustomersAsync();
            return View(customers);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer)
        {
            if (ModelState.IsValid)
            {
                customer.CustomerId = Guid.NewGuid().ToString();
                customer.DateRegistered = DateTime.UtcNow;
                await _tableService.AddCustomerAsync(customer);
                return RedirectToAction(nameof(Index));
            }
            return View(customer);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var customer = await _tableService.GetCustomerAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Customer customer)
        {
            if (id != customer.CustomerId) return NotFound();

            if (ModelState.IsValid)
            {
                var existing = await _tableService.GetCustomerAsync(id);
                if (existing == null) return NotFound();

                customer.PartitionKey = "Customer";
                customer.RowKey = id;
                customer.ETag = existing.ETag;
                customer.Timestamp = existing.Timestamp;

                await _tableService.UpdateCustomerAsync(customer);
                return RedirectToAction(nameof(Index));
            }
            return View(customer);
        }

        public async Task<IActionResult> Delete(string id)
        {
            var customer = await _tableService.GetCustomerAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            await _tableService.DeleteCustomerAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
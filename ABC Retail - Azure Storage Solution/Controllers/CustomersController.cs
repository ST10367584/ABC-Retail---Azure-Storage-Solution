using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.AzureStorage.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CustomersController : Controller
    {
        private readonly ITableStorageService _tableService;
        private readonly IAppLogger _appLogger;
        private readonly ILogger<CustomersController> _logger;

        public CustomersController(
            ITableStorageService tableService,
            IAppLogger appLogger,
            ILogger<CustomersController> logger)
        {
            _tableService = tableService;
            _appLogger = appLogger;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var customers = await _tableService.GetAllCustomersAsync();
            return View(customers);
        }

        public IActionResult Create()
        {
            return View(new Customer());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Customer customer, string? password, string? role)
        {
            // Remove fields that aren't in the form
            ModelState.Remove("CustomerId");
            ModelState.Remove("PartitionKey");
            ModelState.Remove("RowKey");
            ModelState.Remove("ETag");
            ModelState.Remove("Timestamp");
            ModelState.Remove("Password");
            ModelState.Remove("Role");
            ModelState.Remove("DateRegistered");

            if (ModelState.IsValid)
            {
                try
                {
                    customer.CustomerId = Guid.NewGuid().ToString();
                    customer.RowKey = customer.CustomerId;
                    customer.PartitionKey = "Customer";
                    customer.DateRegistered = DateTime.UtcNow;
                    customer.Password = string.IsNullOrEmpty(password) ? "Temp@123" : password;
                    customer.Role = string.IsNullOrEmpty(role) ? "Customer" : role;

                    await _tableService.AddCustomerAsync(customer);

                    await _appLogger.LogAsync("Information", "Customers",
                        $"Customer created: {customer.FirstName} {customer.LastName} ({customer.Email}) as {customer.Role}",
                        User.Identity?.Name);

                    TempData["SuccessMessage"] = $"Customer '{customer.FirstName} {customer.LastName}' created successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await _appLogger.LogAsync("Error", "Customers",
                        $"Error creating customer: {ex.Message}",
                        User.Identity?.Name);
                    ModelState.AddModelError("", $"Error: {ex.Message}");
                }
            }

            foreach (var err in ModelState.Values.SelectMany(v => v.Errors))
                _logger.LogWarning($"Validation: {err.ErrorMessage}");

            return View(customer);
        }

        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var customer = await _tableService.GetCustomerAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Customer customer)
        {
            if (id != customer.CustomerId) return NotFound();

            ModelState.Remove("PartitionKey");
            ModelState.Remove("RowKey");
            ModelState.Remove("ETag");
            ModelState.Remove("Timestamp");
            ModelState.Remove("Password");
            ModelState.Remove("Role");

            if (ModelState.IsValid)
            {
                try
                {
                    var existing = await _tableService.GetCustomerAsync(id);
                    if (existing == null) return NotFound();

                    customer.PartitionKey = "Customer";
                    customer.RowKey = id;
                    customer.ETag = existing.ETag;
                    customer.Timestamp = existing.Timestamp;
                    customer.DateRegistered = existing.DateRegistered;
                    customer.Password = existing.Password; // keep existing
                    customer.Role = existing.Role;         // keep existing

                    await _tableService.UpdateCustomerAsync(customer);

                    await _appLogger.LogAsync("Information", "Customers",
                        $"Customer updated: {customer.FirstName} {customer.LastName} ({customer.Email})",
                        User.Identity?.Name);

                    TempData["SuccessMessage"] = $"Customer '{customer.FirstName} {customer.LastName}' updated successfully!";
                    return RedirectToAction(nameof(Index));
                }
                catch (Exception ex)
                {
                    await _appLogger.LogAsync("Error", "Customers",
                        $"Error updating customer {id}: {ex.Message}",
                        User.Identity?.Name);
                    ModelState.AddModelError("", $"Error: {ex.Message}");
                }
            }

            return View(customer);
        }

        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var customer = await _tableService.GetCustomerAsync(id);
            if (customer == null) return NotFound();
            return View(customer);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var customer = await _tableService.GetCustomerAsync(id);
            await _tableService.DeleteCustomerAsync(id);

            await _appLogger.LogAsync("Warning", "Customers",
                $"Customer deleted: {customer?.FirstName} {customer?.LastName} ({customer?.Email})",
                User.Identity?.Name);

            TempData["SuccessMessage"] = "Customer deleted successfully!";
            return RedirectToAction(nameof(Index));
        }
    }
}
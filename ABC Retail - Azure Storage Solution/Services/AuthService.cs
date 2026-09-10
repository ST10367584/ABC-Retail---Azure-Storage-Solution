using ABCRetail.AzureStorage.Models;
using Azure;
using Azure.Data.Tables;

namespace ABCRetail.AzureStorage.Services
{
    public class AuthService : IAuthService
    {
        private readonly ITableStorageService _tableService;
        private readonly ILogger<AuthService> _logger;

        public AuthService(ITableStorageService tableService, ILogger<AuthService> logger)
        {
            _tableService = tableService;
            _logger = logger;
        }

        public async Task<Customer?> LoginAsync(string email, string password)
        {
            try
            {
                var customers = await _tableService.GetAllCustomersAsync();
                var customer = customers.FirstOrDefault(c =>
                    string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase) &&
                    c.Password == password); // ⚠️ For demo only - use hashing in production

                return customer;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Login error: {ex.Message}");
                return null;
            }
        }

        public async Task<bool> RegisterAsync(Customer customer)
        {
            try
            {
                // Check if email already exists
                if (await CustomerExistsAsync(customer.Email!))
                {
                    return false;
                }

                customer.CustomerId = Guid.NewGuid().ToString();
                customer.RowKey = customer.CustomerId;
                customer.PartitionKey = "Customer";
                customer.DateRegistered = DateTime.UtcNow;

                if (string.IsNullOrEmpty(customer.Role))
                    customer.Role = "Customer";

                await _tableService.AddCustomerAsync(customer);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Registration error: {ex.Message}");
                return false;
            }
        }

        public async Task<Customer?> GetCustomerByEmailAsync(string email)
        {
            var customers = await _tableService.GetAllCustomersAsync();
            return customers.FirstOrDefault(c =>
                string.Equals(c.Email, email, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<bool> CustomerExistsAsync(string email)
        {
            var customer = await GetCustomerByEmailAsync(email);
            return customer != null;
        }
    }
}
using ABCRetail.AzureStorage.Models;

namespace ABCRetail.AzureStorage.Services
{
    public interface ITableStorageService
    {
        Task InitializeAsync();

        // Customer operations
        Task<List<Customer>> GetAllCustomersAsync();
        Task<Customer?> GetCustomerAsync(string customerId);
        Task AddCustomerAsync(Customer customer);
        Task UpdateCustomerAsync(Customer customer);
        Task DeleteCustomerAsync(string customerId);

        // Product operations
        Task<List<Product>> GetAllProductsAsync();
        Task<Product?> GetProductAsync(string productId);
        Task AddProductAsync(Product product);
        Task UpdateProductAsync(Product product);
        Task DeleteProductAsync(string productId);
    }
}
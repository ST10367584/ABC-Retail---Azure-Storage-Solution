using ABCRetail.AzureStorage.Models;

namespace ABCRetail.AzureStorage.Services
{
    public interface IAuthService
    {
        Task<Customer?> LoginAsync(string email, string password);
        Task<bool> RegisterAsync(Customer customer);
        Task<Customer?> GetCustomerByEmailAsync(string email);
        Task<bool> CustomerExistsAsync(string email);
    }
}
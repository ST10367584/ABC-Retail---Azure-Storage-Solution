using ABCRetail.AzureStorage.Models;
using Azure;
using Azure.Data.Tables;

namespace ABCRetail.AzureStorage.Services
{
    public class TableStorageService : ITableStorageService
    {
        private readonly TableServiceClient _tableServiceClient;
        private readonly IConfiguration _configuration;
        private TableClient? _customerTable;
        private TableClient? _productTable;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;

        public TableStorageService(TableServiceClient tableServiceClient, IConfiguration configuration)
        {
            _tableServiceClient = tableServiceClient;
            _configuration = configuration;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                var customerTableName = _configuration["AzureStorage:TableNames:Customers"] ?? "Customers";
                var productTableName = _configuration["AzureStorage:TableNames:Products"] ?? "Products";

                _customerTable = _tableServiceClient.GetTableClient(customerTableName);
                await _customerTable.CreateIfNotExistsAsync();

                _productTable = _tableServiceClient.GetTableClient(productTableName);
                await _productTable.CreateIfNotExistsAsync();

                _isInitialized = true;
                Console.WriteLine($"Tables '{customerTableName}' and '{productTableName}' initialized successfully.");
            }
            finally
            {
                _initLock.Release();
            }
        }

        private async Task EnsureInitializedAsync()
        {
            if (!_isInitialized)
            {
                await InitializeAsync();
            }
        }

        // Customer Operations
        public async Task<List<Customer>> GetAllCustomersAsync()
        {
            await EnsureInitializedAsync();

            var customers = new List<Customer>();
            if (_customerTable == null) return customers;

            await foreach (var customer in _customerTable.QueryAsync<Customer>())
            {
                customers.Add(customer);
            }
            return customers;
        }

        public async Task<Customer?> GetCustomerAsync(string customerId)
        {
            await EnsureInitializedAsync();

            if (_customerTable == null) return null;
            try
            {
                var response = await _customerTable.GetEntityAsync<Customer>("Customer", customerId);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task AddCustomerAsync(Customer customer)
        {
            await EnsureInitializedAsync();

            if (_customerTable == null) return;
            customer.PartitionKey = "Customer";
            customer.RowKey = customer.CustomerId ?? Guid.NewGuid().ToString();
            await _customerTable.AddEntityAsync(customer);
        }

        public async Task UpdateCustomerAsync(Customer customer)
        {
            await EnsureInitializedAsync();

            if (_customerTable == null) return;
            await _customerTable.UpdateEntityAsync(customer, customer.ETag, TableUpdateMode.Replace);
        }

        public async Task DeleteCustomerAsync(string customerId)
        {
            await EnsureInitializedAsync();

            if (_customerTable == null) return;
            await _customerTable.DeleteEntityAsync("Customer", customerId);
        }

        // Product Operations
        public async Task<List<Product>> GetAllProductsAsync()
        {
            await EnsureInitializedAsync();

            var products = new List<Product>();
            if (_productTable == null) return products;

            await foreach (var product in _productTable.QueryAsync<Product>())
            {
                products.Add(product);
            }
            return products;
        }

        public async Task<Product?> GetProductAsync(string productId)
        {
            await EnsureInitializedAsync();

            if (_productTable == null) return null;
            try
            {
                var response = await _productTable.GetEntityAsync<Product>("Product", productId);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task AddProductAsync(Product product)
        {
            await EnsureInitializedAsync();

            if (_productTable == null) return;
            product.PartitionKey = "Product";
            product.RowKey = product.ProductId ?? Guid.NewGuid().ToString();
            await _productTable.AddEntityAsync(product);
        }

        public async Task UpdateProductAsync(Product product)
        {
            await EnsureInitializedAsync();

            if (_productTable == null) return;
            await _productTable.UpdateEntityAsync(product, product.ETag, TableUpdateMode.Replace);
        }

        public async Task DeleteProductAsync(string productId)
        {
            await EnsureInitializedAsync();

            if (_productTable == null) return;
            await _productTable.DeleteEntityAsync("Product", productId);
        }
    }
}
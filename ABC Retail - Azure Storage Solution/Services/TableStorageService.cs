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
        private readonly object _lock = new object();

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

                Console.WriteLine($"===== INITIALIZING TABLES =====");
                Console.WriteLine($"Customer Table: {customerTableName}");
                Console.WriteLine($"Product Table: {productTableName}");

                // Initialize Customer Table
                _customerTable = _tableServiceClient.GetTableClient(customerTableName);
                await _customerTable.CreateIfNotExistsAsync();
                Console.WriteLine($"✅ Customer table '{customerTableName}' ready");

                // Initialize Product Table
                _productTable = _tableServiceClient.GetTableClient(productTableName);
                await _productTable.CreateIfNotExistsAsync();
                Console.WriteLine($"✅ Product table '{productTableName}' ready");

                _isInitialized = true;
                Console.WriteLine($"✅ Tables initialized successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error initializing tables: {ex.Message}");
                throw;
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
                Console.WriteLine("⚠️ Tables not initialized, initializing now...");
                await InitializeAsync();
            }

            // Double-check that tables are accessible
            if (_productTable == null)
            {
                Console.WriteLine("⚠️ Product table is null, reinitializing...");
                await InitializeAsync();
            }
        }

        // Customer Operations
        public async Task<List<Customer>> GetAllCustomersAsync()
        {
            await EnsureInitializedAsync();

            var customers = new List<Customer>();
            if (_customerTable == null)
            {
                Console.WriteLine("❌ Customer table is null");
                return customers;
            }

            try
            {
                Console.WriteLine("===== GETTING ALL CUSTOMERS =====");
                await foreach (var customer in _customerTable.QueryAsync<Customer>())
                {
                    customers.Add(customer);
                }
                Console.WriteLine($"Total customers retrieved: {customers.Count}");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"⚠️ Customer table not found, creating...");
                await _customerTable.CreateIfNotExistsAsync();
                // Retry
                await foreach (var customer in _customerTable.QueryAsync<Customer>())
                {
                    customers.Add(customer);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting customers: {ex.Message}");
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

            if (_productTable == null)
            {
                throw new InvalidOperationException(
                    "Product table client is null.");
            }

            try
            {
                Console.WriteLine("===== GETTING ALL PRODUCTS =====");
                Console.WriteLine($"Table: {_productTable.Name}");

                var products = new List<Product>();

                await foreach (var product in _productTable.QueryAsync<Product>())
                {
                    Console.WriteLine(
                        $"Found product: {product.Name} | " +
                        $"ID: {product.ProductId} | " +
                        $"RowKey: {product.RowKey} | " +
                        $"Price: {product.Price}");

                    products.Add(product);
                }

                Console.WriteLine(
                    $"===== TOTAL PRODUCTS: {products.Count} =====");

                return products;
            }
            catch (Exception ex)
            {
                Console.WriteLine(
                    $"❌ ERROR GETTING PRODUCTS: {ex}");

                throw;
            }
        }

        public async Task<Product?> GetProductAsync(string productId)
        {
            await EnsureInitializedAsync();

            if (_productTable == null)
            {
                Console.WriteLine("❌ Product table is null");
                return null;
            }

            try
            {
                Console.WriteLine($"===== GETTING PRODUCT: {productId} =====");
                var response = await _productTable.GetEntityAsync<Product>("Product", productId);
                Console.WriteLine($"Retrieved product: {response.Value.Name}, Price: {response.Value.Price}");
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"Product {productId} not found");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting product: {ex.Message}");
                return null;
            }
        }

        public async Task AddProductAsync(Product product)
        {
            // Ensure initialization
            await EnsureInitializedAsync();

            // Ensure table exists
            if (_productTable == null)
            {
                Console.WriteLine("❌ Product table is null, reinitializing...");
                await InitializeAsync();
                if (_productTable == null)
                {
                    throw new InvalidOperationException("Product table could not be initialized");
                }
            }

            // Ensure required fields are set
            product.PartitionKey = "Product";
            product.RowKey = product.ProductId ?? Guid.NewGuid().ToString();

            Console.WriteLine($"===== SAVING PRODUCT =====");
            Console.WriteLine($"Name: {product.Name}");
            Console.WriteLine($"Price: {product.Price}");
            Console.WriteLine($"Stock: {product.StockQuantity}");
            Console.WriteLine($"RowKey: {product.RowKey}");
            Console.WriteLine($"PartitionKey: {product.PartitionKey}");

            try
            {
                // Try to save
                await _productTable.AddEntityAsync(product);
                Console.WriteLine($"✅ Product saved successfully!");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"⚠️ Table not found, attempting to create and retry...");
                // Create the table
                await _productTable.CreateIfNotExistsAsync();
                // Retry the save
                await _productTable.AddEntityAsync(product);
                Console.WriteLine($"✅ Product saved successfully on retry!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error saving product: {ex.Message}");
                throw;
            }

            // Verify the product was saved
            try
            {
                var saved = await _productTable.GetEntityAsync<Product>("Product", product.RowKey);
                Console.WriteLine($"✅ Verified saved product: {saved.Value.Name}, Price: {saved.Value.Price}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"⚠️ Could not verify saved product: {ex.Message}");
            }
        }

        public async Task UpdateProductAsync(Product product)
        {
            await EnsureInitializedAsync();

            if (_productTable == null)
            {
                Console.WriteLine("❌ Product table is null");
                return;
            }

            Console.WriteLine($"===== UPDATING PRODUCT =====");
            Console.WriteLine($"Name: {product.Name}");
            Console.WriteLine($"Price: {product.Price}");
            Console.WriteLine($"RowKey: {product.RowKey}");

            try
            {
                await _productTable.UpdateEntityAsync(product, product.ETag, TableUpdateMode.Replace);
                Console.WriteLine($"✅ Product updated successfully!");

                // Verify the update
                var updated = await _productTable.GetEntityAsync<Product>("Product", product.RowKey);
                Console.WriteLine($"✅ Verified updated product: {updated.Value.Name}, Price: {updated.Value.Price}");
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                Console.WriteLine($"⚠️ Table not found, attempting to create and retry...");
                await _productTable.CreateIfNotExistsAsync();
                await _productTable.UpdateEntityAsync(product, product.ETag, TableUpdateMode.Replace);
                Console.WriteLine($"✅ Product updated successfully on retry!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error updating product: {ex.Message}");
                throw;
            }
        }

        public async Task DeleteProductAsync(string productId)
        {
            await EnsureInitializedAsync();

            if (_productTable == null) return;
            await _productTable.DeleteEntityAsync("Product", productId);
            Console.WriteLine($"Product deleted: {productId}");
        }
    }
}
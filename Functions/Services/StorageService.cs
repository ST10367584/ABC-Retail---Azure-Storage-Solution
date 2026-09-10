using ABCRetail.Functions.Models;
using Azure;
using Azure.Data.Tables;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using System.Text.Json;

namespace ABCRetail.Functions.Services
{
    public class StorageService : IStorageService
    {
        private readonly string _connectionString;
        private readonly TableServiceClient _tableService;
        private readonly BlobServiceClient _blobService;
        private readonly QueueServiceClient _queueService;
        private readonly ShareServiceClient _fileService;

        public StorageService(string connectionString)
        {
            _connectionString = connectionString;
            _tableService = new TableServiceClient(connectionString);
            _blobService = new BlobServiceClient(connectionString);
            _queueService = new QueueServiceClient(connectionString);
            _fileService = new ShareServiceClient(connectionString);
        }

        public async Task InitializeAsync()
        {
            try
            {
                // Create tables
                var tables = new[] { "Customers", "Products" };
                foreach (var tableName in tables)
                {
                    var tableClient = _tableService.GetTableClient(tableName);
                    await tableClient.CreateIfNotExistsAsync();
                }

                // Create blob container
                var containerClient = _blobService.GetBlobContainerClient("productimages");
                await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

                // Create queue
                var queueClient = _queueService.GetQueueClient("orderprocessing");
                await queueClient.CreateIfNotExistsAsync();

                // Create file share
                var shareClient = _fileService.GetShareClient("logs");
                await shareClient.CreateIfNotExistsAsync();
                var directoryClient = shareClient.GetDirectoryClient("application-logs");
                await directoryClient.CreateIfNotExistsAsync();

                Console.WriteLine("✅ Storage services initialized successfully!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error initializing storage: {ex.Message}");
                throw;
            }
        }

        // Table Storage Operations
        public async Task AddEntityAsync<T>(T entity, string tableName) where T : class, ITableEntity
        {
            var tableClient = _tableService.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();
            await tableClient.AddEntityAsync(entity);
        }

        public async Task<List<T>> GetAllEntitiesAsync<T>(string tableName) where T : class, ITableEntity, new()
        {
            var tableClient = _tableService.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();

            var results = new List<T>();
            await foreach (var entity in tableClient.QueryAsync<T>())
            {
                results.Add(entity);
            }
            return results;
        }

        public async Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey) where T : class, ITableEntity, new()
        {
            var tableClient = _tableService.GetTableClient(tableName);
            await tableClient.CreateIfNotExistsAsync();

            try
            {
                var response = await tableClient.GetEntityAsync<T>(partitionKey, rowKey);
                return response.Value;
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return null;
            }
        }

        public async Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey)
        {
            var tableClient = _tableService.GetTableClient(tableName);
            await tableClient.DeleteEntityAsync(partitionKey, rowKey);
        }

        // Blob Storage Operations
        public async Task<string> UploadBlobAsync(Stream stream, string containerName, string blobName, string contentType)
        {
            var containerClient = _blobService.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);

            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = contentType });

            return blobClient.Uri.ToString();
        }

        public async Task<Stream?> DownloadBlobAsync(string containerName, string blobName)
        {
            var containerClient = _blobService.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            if (!await blobClient.ExistsAsync())
                return null;

            var response = await blobClient.DownloadStreamingAsync();
            return response.Value.Content;
        }

        public async Task DeleteBlobAsync(string containerName, string blobName)
        {
            var containerClient = _blobService.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        // Queue Storage Operations
        public async Task SendQueueMessageAsync(string queueName, string message)
        {
            var queueClient = _queueService.GetQueueClient(queueName);
            await queueClient.CreateIfNotExistsAsync();
            await queueClient.SendMessageAsync(message);
        }

        public async Task<string?> ReceiveQueueMessageAsync(string queueName)
        {
            var queueClient = _queueService.GetQueueClient(queueName);
            await queueClient.CreateIfNotExistsAsync();

            var response = await queueClient.ReceiveMessagesAsync(maxMessages: 1);
            var message = response.Value.FirstOrDefault();

            if (message == null)
                return null;

            await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
            return message.MessageText;
        }

        public async Task<int> GetQueueLengthAsync(string queueName)
        {
            var queueClient = _queueService.GetQueueClient(queueName);
            var properties = await queueClient.GetPropertiesAsync();
            return properties.Value.ApproximateMessagesCount;
        }

        // File Storage Operations
        public async Task<string> WriteFileAsync(string shareName, string directoryName, string fileName, string content)
        {
            var shareClient = _fileService.GetShareClient(shareName);
            await shareClient.CreateIfNotExistsAsync();

            var directoryClient = shareClient.GetDirectoryClient(directoryName);
            await directoryClient.CreateIfNotExistsAsync();

            var fileClient = directoryClient.GetFileClient(fileName);

            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            using var stream = new MemoryStream(bytes);

            await fileClient.CreateAsync(stream.Length);
            await fileClient.UploadRangeAsync(new Azure.HttpRange(0, stream.Length), stream);

            return fileName;
        }

        public async Task<string?> ReadFileAsync(string shareName, string directoryName, string fileName)
        {
            var shareClient = _fileService.GetShareClient(shareName);
            var directoryClient = shareClient.GetDirectoryClient(directoryName);
            var fileClient = directoryClient.GetFileClient(fileName);

            if (!await fileClient.ExistsAsync())
                return null;

            var response = await fileClient.DownloadAsync();
            using var reader = new StreamReader(response.Value.Content);
            return await reader.ReadToEndAsync();
        }

        public async Task DeleteFileAsync(string shareName, string directoryName, string fileName)
        {
            var shareClient = _fileService.GetShareClient(shareName);
            var directoryClient = shareClient.GetDirectoryClient(directoryName);
            var fileClient = directoryClient.GetFileClient(fileName);
            await fileClient.DeleteIfExistsAsync();
        }
    }
}
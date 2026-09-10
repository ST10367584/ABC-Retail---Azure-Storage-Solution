using Azure.Data.Tables;

namespace ABCRetail.Functions.Services
{
    public interface IStorageService
    {
        Task InitializeAsync();
        Task AddEntityAsync<T>(T entity, string tableName) where T : class, ITableEntity;
        Task<List<T>> GetAllEntitiesAsync<T>(string tableName) where T : class, ITableEntity, new();
        Task<T?> GetEntityAsync<T>(string tableName, string partitionKey, string rowKey) where T : class, ITableEntity, new();
        Task DeleteEntityAsync(string tableName, string partitionKey, string rowKey);

        Task<string> UploadBlobAsync(Stream stream, string containerName, string blobName, string contentType);
        Task<Stream?> DownloadBlobAsync(string containerName, string blobName);
        Task DeleteBlobAsync(string containerName, string blobName);

        Task SendQueueMessageAsync(string queueName, string message);
        Task<string?> ReceiveQueueMessageAsync(string queueName);
        Task<int> GetQueueLengthAsync(string queueName);

        Task<string> WriteFileAsync(string shareName, string directoryName, string fileName, string content);
        Task<string?> ReadFileAsync(string shareName, string directoryName, string fileName);
        Task DeleteFileAsync(string shareName, string directoryName, string fileName);
    }
}
using ABCRetail.AzureStorage.Models;

namespace ABCRetail.AzureStorage.Services
{
    public interface IFileStorageService
    {
        Task InitializeAsync();
        Task<string> WriteLogAsync(LogEntry log);
        Task<string> ReadLogAsync(string logId);
        Task<List<LogEntry>> ListLogsAsync(int maxEntries = 50);
        Task DeleteLogAsync(string logId);
    }
}
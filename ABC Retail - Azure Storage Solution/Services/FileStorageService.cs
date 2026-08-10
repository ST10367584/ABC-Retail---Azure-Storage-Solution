using ABCRetail.AzureStorage.Models;
using Azure;
using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;

namespace ABCRetail.AzureStorage.Services
{
    public class FileStorageService : IFileStorageService
    {
        private readonly ShareServiceClient _shareServiceClient;
        private readonly IConfiguration _configuration;
        private ShareClient? _shareClient;
        private ShareDirectoryClient? _logDirectory;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;

        public FileStorageService(ShareServiceClient shareServiceClient, IConfiguration configuration)
        {
            _shareServiceClient = shareServiceClient;
            _configuration = configuration;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                var shareName = _configuration["AzureStorage:FileShare"] ?? "logs";
                _shareClient = _shareServiceClient.GetShareClient(shareName);
                await _shareClient.CreateIfNotExistsAsync();

                _logDirectory = _shareClient.GetDirectoryClient("application-logs");
                await _logDirectory.CreateIfNotExistsAsync();

                _isInitialized = true;
                Console.WriteLine($"File share '{shareName}' initialized successfully.");
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

        public async Task<string> WriteLogAsync(LogEntry log)
        {
            await EnsureInitializedAsync();

            if (_logDirectory == null)
                throw new InvalidOperationException("File share not initialized");

            var fileName = $"{log.LogId}.log";
            var fileClient = _logDirectory.GetFileClient(fileName);

            var content = log.ToLogString();
            using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(content));
            await fileClient.CreateAsync(stream.Length);
            await fileClient.UploadRangeAsync(new HttpRange(0, stream.Length), stream);

            return fileName;
        }

        public async Task<string> ReadLogAsync(string logId)
        {
            await EnsureInitializedAsync();

            if (_logDirectory == null) return string.Empty;

            var fileName = $"{logId}.log";
            var fileClient = _logDirectory.GetFileClient(fileName);

            if (!await fileClient.ExistsAsync()) return string.Empty;

            var response = await fileClient.DownloadAsync();
            using var reader = new StreamReader(response.Value.Content);
            return await reader.ReadToEndAsync();
        }

        public async Task<List<LogEntry>> ListLogsAsync(int maxEntries = 50)
        {
            await EnsureInitializedAsync();

            var logs = new List<LogEntry>();
            if (_logDirectory == null) return logs;

            var files = _logDirectory.GetFilesAndDirectoriesAsync();
            int count = 0;

            await foreach (var item in files)
            {
                if (count >= maxEntries) break;
                if (item.IsDirectory) continue;

                var logId = Path.GetFileNameWithoutExtension(item.Name);
                var content = await ReadLogAsync(logId);

                if (!string.IsNullOrEmpty(content))
                {
                    var log = ParseLogEntry(content, logId);
                    logs.Add(log);
                }
                count++;
            }

            return logs.OrderByDescending(l => l.Timestamp).ToList();
        }

        public async Task DeleteLogAsync(string logId)
        {
            await EnsureInitializedAsync();

            if (_logDirectory == null) return;

            var fileName = $"{logId}.log";
            var fileClient = _logDirectory.GetFileClient(fileName);
            await fileClient.DeleteIfExistsAsync();
        }

        private LogEntry ParseLogEntry(string content, string logId)
        {
            var log = new LogEntry
            {
                LogId = logId
            };

            try
            {
                var parts = content.Split(new[] { ']' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 4)
                {
                    var timestampStr = parts[0].TrimStart('[').Trim();
                    if (DateTime.TryParse(timestampStr, out var timestamp))
                        log.Timestamp = timestamp;

                    log.Level = parts[1].TrimStart('[').Trim();
                    log.Source = parts[2].TrimStart('[').Trim();
                    log.Message = parts[3].Trim();
                }
                else
                {
                    log.Message = content;
                }
            }
            catch
            {
                log.Message = content;
            }

            return log;
        }
    }
}
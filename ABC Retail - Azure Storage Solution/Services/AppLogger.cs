using ABCRetail.AzureStorage.Models;

namespace ABCRetail.AzureStorage.Services
{
    public class AppLogger : IAppLogger
    {
        private readonly IFileStorageService _fileService;
        private readonly ILogger<AppLogger> _logger;

        public AppLogger(IFileStorageService fileService, ILogger<AppLogger> logger)
        {
            _fileService = fileService;
            _logger = logger;
        }

        public async Task LogAsync(string level, string source, string message, string? userId = null)
        {
            try
            {
                var entry = new LogEntry
                {
                    LogId = Guid.NewGuid().ToString(),
                    Application = "ABC Retail",
                    Level = level,
                    Source = source,
                    Message = message,
                    UserId = userId ?? "System",
                    Timestamp = DateTime.UtcNow
                };

                _logger.LogInformation($"[{level}] [{source}] {message}");

                await _fileService.WriteLogAsync(entry);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Failed to write log: {ex.Message}");
            }
        }
    }
}
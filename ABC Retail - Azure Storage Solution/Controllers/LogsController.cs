using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.AzureStorage.Controllers
{
    [Authorize(Roles = "Admin")]
    public class LogsController : Controller
    {
        private readonly IFileStorageService _fileService;
        private readonly IAppLogger _appLogger;
        private readonly ILogger<LogsController> _logger;

        public LogsController(IFileStorageService fileService, IAppLogger appLogger, ILogger<LogsController> logger)
        {
            _fileService = fileService;
            _appLogger = appLogger;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var logs = await _fileService.ListLogsAsync(50);
            return View(logs);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateLogEntry(string message, string level = "Information", string source = "Application")
        {
            var logEntry = new LogEntry
            {
                Message = message,
                Level = level,
                Source = source,
                Application = "ABC Retail",
                UserId = User.Identity?.Name ?? "System"
            };

            var fileName = await _fileService.WriteLogAsync(logEntry);
            TempData["LogCreated"] = $"Log entry created: {fileName}";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> View(string id)
        {
            var content = await _fileService.ReadLogAsync(id);
            if (string.IsNullOrEmpty(content)) return NotFound();

            ViewBag.LogId = id;
            ViewBag.Content = content;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            await _fileService.DeleteLogAsync(id);

            await _appLogger.LogAsync("Warning", "LogsController",
                $"Log deleted: {id}",
                User.Identity?.Name);

            TempData["LogDeleted"] = $"Log {id} deleted.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTestLogs()
        {
            var messages = new[]
            {
                "Application started successfully",
                "User logged in: admin@example.com",
                "Order #ORD-001 processed successfully",
                "Product inventory updated for SKU: PROD-123",
                "Payment processed for order #ORD-002",
                "User logged out: admin@example.com",
                "Database backup completed",
                "Cache cleared for product catalog",
                "New customer registered: johndoe@example.com",
                "Order #ORD-003 shipped to customer"
            };

            var levels = new[] { "Information", "Warning", "Error", "Information", "Information", "Warning", "Information", "Information", "Information", "Information" };
            var sources = new[] { "Startup", "Auth", "Order", "Inventory", "Payment", "Auth", "Backup", "Cache", "Customer", "Shipping" };

            for (int i = 0; i < messages.Length; i++)
            {
                var log = new LogEntry
                {
                    Message = messages[i],
                    Level = levels[i],
                    Source = sources[i],
                    Application = "ABC Retail",
                    UserId = "System"
                };
                await _fileService.WriteLogAsync(log);
            }

            await _appLogger.LogAsync("Information", "LogsController",
                $"Generated {messages.Length} test log entries",
                User.Identity?.Name);

            TempData["TestLogsCreated"] = $"Created {messages.Length} test log entries.";
            return RedirectToAction(nameof(Index));
        }
    }
}
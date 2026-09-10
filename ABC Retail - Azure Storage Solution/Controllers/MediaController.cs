using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.AzureStorage.Controllers
{
    [Authorize(Roles = "Admin")]
    public class MediaController : Controller
    {
        private readonly IBlobStorageService _blobService;
        private readonly IAppLogger _appLogger;

        public MediaController(IBlobStorageService blobService, IAppLogger appLogger)
        {
            _blobService = blobService;
            _appLogger = appLogger;
        }

        public async Task<IActionResult> Index()
        {
            var images = await _blobService.ListImagesAsync();
            return View(images);
        }

        public IActionResult Upload()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Upload(IFormFile file, string? subFolder)
        {
            if (file == null || file.Length == 0)
            {
                ModelState.AddModelError("", "Please select a file to upload.");
                return View();
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();

            if (!allowedExtensions.Contains(extension))
            {
                ModelState.AddModelError("", "Only image files are allowed.");
                return View();
            }

            var blobName = await _blobService.UploadImageAsync(file, subFolder);
            var url = _blobService.GetImageUrl(blobName);

            await _appLogger.LogAsync("Information", "Media",
                $"Image uploaded: {file.FileName} ({file.Length} bytes) as {blobName}",
                User.Identity?.Name);

            TempData["UploadSuccess"] = $"File uploaded successfully. URL: {url}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string blobName)
        {
            try
            {
                if (string.IsNullOrEmpty(blobName))
                {
                    TempData["DeleteError"] = "Invalid blob name.";
                    return RedirectToAction(nameof(Index));
                }

                await _blobService.DeleteImageAsync(blobName);

                await _appLogger.LogAsync("Warning", "Media",
                    $"Image deleted: {blobName}",
                    User.Identity?.Name);

                TempData["DeleteSuccess"] = $"Image '{blobName}' deleted successfully.";
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error", "Media",
                    $"Failed to delete image '{blobName}': {ex.Message}",
                    User.Identity?.Name);
                TempData["DeleteError"] = $"Failed to delete image: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Download(string blobName)
        {
            var stream = await _blobService.DownloadImageAsync(blobName);
            if (stream == null) return NotFound();

            var fileName = Path.GetFileName(blobName);

            await _appLogger.LogAsync("Information", "Media",
                $"Image downloaded: {blobName}",
                User.Identity?.Name);

            return File(stream, "application/octet-stream", fileName);
        }
    }
}
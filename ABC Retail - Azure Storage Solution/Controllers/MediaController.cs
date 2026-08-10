using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.AzureStorage.Controllers
{
    public class MediaController : Controller
    {
        private readonly IBlobStorageService _blobService;

        public MediaController(IBlobStorageService blobService)
        {
            _blobService = blobService;
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
                ModelState.AddModelError("", "Only image files (jpg, jpeg, png, gif, bmp, webp) are allowed.");
                return View();
            }

            var blobName = await _blobService.UploadImageAsync(file, subFolder);
            var url = _blobService.GetImageUrl(blobName);

            TempData["UploadSuccess"] = $"File uploaded successfully. URL: {url}";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> Delete(string blobName)
        {
            await _blobService.DeleteImageAsync(blobName);
            TempData["DeleteSuccess"] = "Image deleted successfully.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Download(string blobName)
        {
            var stream = await _blobService.DownloadImageAsync(blobName);
            if (stream == null) return NotFound();

            var fileName = Path.GetFileName(blobName);
            return File(stream, "application/octet-stream", fileName);
        }
    }
}
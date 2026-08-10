using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.AzureStorage.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ITableStorageService _tableService;
        private readonly IBlobStorageService _blobService;

        public ProductsController(ITableStorageService tableService, IBlobStorageService blobService)
        {
            _tableService = tableService;
            _blobService = blobService;
        }

        public async Task<IActionResult> Index()
        {
            var products = await _tableService.GetAllProductsAsync();
            return View(products);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Product product, IFormFile? imageFile)
        {
            if (ModelState.IsValid)
            {
                product.ProductId = Guid.NewGuid().ToString();
                product.DateAdded = DateTime.UtcNow;

                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageName = await _blobService.UploadImageAsync(imageFile, "products");
                    product.ImageUrl = _blobService.GetImageUrl(imageName);
                }

                await _tableService.AddProductAsync(product);
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        public async Task<IActionResult> Edit(string id)
        {
            var product = await _tableService.GetProductAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(string id, Product product, IFormFile? imageFile)
        {
            if (id != product.ProductId) return NotFound();

            if (ModelState.IsValid)
            {
                var existing = await _tableService.GetProductAsync(id);
                if (existing == null) return NotFound();

                if (imageFile != null && imageFile.Length > 0)
                {
                    var imageName = await _blobService.UploadImageAsync(imageFile, "products");
                    product.ImageUrl = _blobService.GetImageUrl(imageName);
                }
                else
                {
                    product.ImageUrl = existing.ImageUrl;
                }

                product.PartitionKey = "Product";
                product.RowKey = id;
                product.ETag = existing.ETag;
                product.Timestamp = existing.Timestamp;
                product.DateAdded = existing.DateAdded;

                await _tableService.UpdateProductAsync(product);
                return RedirectToAction(nameof(Index));
            }
            return View(product);
        }

        public async Task<IActionResult> Delete(string id)
        {
            var product = await _tableService.GetProductAsync(id);
            if (product == null) return NotFound();
            return View(product);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            var product = await _tableService.GetProductAsync(id);
            if (product != null && !string.IsNullOrEmpty(product.ImageUrl))
            {
                // Extract blob name from URL and delete
                var uri = new Uri(product.ImageUrl);
                var blobName = uri.Segments.Last();
                await _blobService.DeleteImageAsync($"products/{blobName}");
            }
            await _tableService.DeleteProductAsync(id);
            return RedirectToAction(nameof(Index));
        }
    }
}
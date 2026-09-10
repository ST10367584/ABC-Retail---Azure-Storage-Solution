using ABCRetail.AzureStorage.Models;
using ABCRetail.AzureStorage.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ABCRetail.AzureStorage.Controllers
{
    public class ProductsController : Controller
    {
        private readonly ITableStorageService _tableService;
        private readonly IBlobStorageService _blobService;
        private readonly IAppLogger _appLogger;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            ITableStorageService tableService,
            IBlobStorageService blobService,
            IAppLogger appLogger,
            ILogger<ProductsController> logger)
        {
            _tableService = tableService;
            _blobService = blobService;
            _appLogger = appLogger;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            try
            {
                var products = await _tableService.GetAllProductsAsync();

                _logger.LogInformation(
    "PRODUCT COUNT = {Count}",
    products.Count);

                foreach (var product in products)
                {
                    _logger.LogInformation(
                        "PRODUCT: {Id} | {Name} | R{Price}",
                        product.ProductId,
                        product.Name,
                        product.Price);
                }

                await _appLogger.LogAsync(
                    "Information",
                    "Products",
                    $"Products list viewed ({products.Count} products shown)",
                    User.Identity?.Name ?? "Guest");

                return View(products);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ERROR loading products from Azure Table Storage");

                await _appLogger.LogAsync(
                    "Error",
                    "Products",
                    $"Error loading products: {ex.Message}",
                    User.Identity?.Name ?? "Guest");

                // IMPORTANT: show the actual error during debugging
                return Content($"ERROR LOADING PRODUCTS: {ex.Message}");
            }
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create()
        {
            await _appLogger.LogAsync("Information", "Products",
                "Opened Create Product page",
                User.Identity?.Name);

            return View(new Product());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Create([Bind("Name,Description,Price,StockQuantity,Category")] Product product, IFormFile? imageFile)
        {
            try
            {
                ModelState.Remove("ProductId");
                ModelState.Remove("PartitionKey");
                ModelState.Remove("RowKey");
                ModelState.Remove("ETag");
                ModelState.Remove("Timestamp");
                ModelState.Remove("DateAdded");
                ModelState.Remove("ImageUrl");

                if (ModelState.IsValid)
                {
                    product.ProductId = Guid.NewGuid().ToString();
                    product.RowKey = product.ProductId;
                    product.PartitionKey = "Product";
                    product.DateAdded = DateTime.UtcNow;

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        var imageName =
                            await _blobService.UploadImageAsync(imageFile, "products");

                        product.ImageUrl =
                            _blobService.GetImageUrl(imageName);

                        await _appLogger.LogAsync(
                            "Information",
                            "Products",
                            $"Image uploaded for product '{product.Name}': {imageFile.FileName}",
                            User.Identity?.Name);
                    }

                    await _tableService.AddProductAsync(product);

                    TempData["SuccessMessage"] =
                        $"Product '{product.Name}' created successfully!";

                    return RedirectToAction(nameof(Index));
                }

                // Log validation failures
                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                await _appLogger.LogAsync("Warning", "Products",
                    $"Product creation failed - validation errors: {errors}",
                    User.Identity?.Name);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error", "Products",
                    $"Error creating product '{product.Name}': {ex.Message}",
                    User.Identity?.Name);
                ModelState.AddModelError("", $"Error creating product: {ex.Message}");
            }

            return View(product);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            try
            {
                var product = await _tableService.GetProductAsync(id);
                if (product == null)
                {
                    await _appLogger.LogAsync("Warning", "Products",
                        $"Attempted to edit non-existent product: {id}",
                        User.Identity?.Name);
                    return NotFound();
                }

                await _appLogger.LogAsync("Information", "Products",
                    $"Opened Edit page for product: '{product.Name}' (ID: {id})",
                    User.Identity?.Name);

                return View(product);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error", "Products",
                    $"Error loading product for edit: {id} - {ex.Message}",
                    User.Identity?.Name);
                return NotFound();
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Edit(string id, [Bind("ProductId,Name,Description,Price,StockQuantity,Category")] Product product, IFormFile? imageFile)
        {
            if (id != product.ProductId) return NotFound();

            try
            {
                ModelState.Remove("PartitionKey");
                ModelState.Remove("RowKey");
                ModelState.Remove("ETag");
                ModelState.Remove("Timestamp");
                ModelState.Remove("DateAdded");
                ModelState.Remove("ImageUrl");

                if (ModelState.IsValid)
                {
                    var existing = await _tableService.GetProductAsync(id);
                    if (existing == null) return NotFound();

                    // Store old values for logging
                    var oldName = existing.Name;
                    var oldPrice = existing.Price;
                    var oldStock = existing.StockQuantity;
                    var oldCategory = existing.Category;

                    product.PartitionKey = "Product";
                    product.RowKey = id;
                    product.ETag = existing.ETag;
                    product.Timestamp = existing.Timestamp;
                    product.DateAdded = existing.DateAdded;

                    if (imageFile != null && imageFile.Length > 0)
                    {
                        if (!string.IsNullOrEmpty(existing.ImageUrl))
                        {
                            try
                            {
                                var uri = new Uri(existing.ImageUrl);
                                var blobName = uri.Segments.Last();
                                await _blobService.DeleteImageAsync($"products/{blobName}");

                                await _appLogger.LogAsync("Information", "Products",
                                    $"Old image deleted for product '{existing.Name}'",
                                    User.Identity?.Name);
                            }
                            catch { }
                        }

                        var imageName = await _blobService.UploadImageAsync(imageFile, "products");
                        product.ImageUrl = _blobService.GetImageUrl(imageName);

                        await _appLogger.LogAsync("Information", "Products",
                            $"New image uploaded for product '{product.Name}': {imageFile.FileName}",
                            User.Identity?.Name);
                    }
                    else
                    {
                        product.ImageUrl = existing.ImageUrl;
                    }

                    await _tableService.UpdateProductAsync(product);

                    // Log all changes
                    var changes = new List<string>();
                    if (oldName != product.Name) changes.Add($"Name: '{oldName}' → '{product.Name}'");
                    if (oldPrice != product.Price) changes.Add($"Price: R{oldPrice:F2} → R{product.Price:F2}");
                    if (oldStock != product.StockQuantity) changes.Add($"Stock: {oldStock} → {product.StockQuantity}");
                    if (oldCategory != product.Category) changes.Add($"Category: '{oldCategory}' → '{product.Category}'");

                    var changesSummary = changes.Any() ? string.Join(" | ", changes) : "No field changes";

                    await _appLogger.LogAsync("Information", "Products",
                        $"Product UPDATED: '{product.Name}' (ID: {id}) | Changes: {changesSummary}",
                        User.Identity?.Name);

                    TempData["SuccessMessage"] = $"Product '{product.Name}' updated successfully!";
                    return RedirectToAction(nameof(Index));
                }

                var errors = string.Join("; ", ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));
                await _appLogger.LogAsync("Warning", "Products",
                    $"Product update failed - validation errors: {errors}",
                    User.Identity?.Name);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error", "Products",
                    $"Error updating product {id}: {ex.Message}",
                    User.Identity?.Name);
                ModelState.AddModelError("", $"Error updating product: {ex.Message}");
            }

            return View(product);
        }

        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            try
            {
                var product = await _tableService.GetProductAsync(id);
                if (product == null)
                {
                    await _appLogger.LogAsync("Warning", "Products",
                        $"Attempted to delete non-existent product: {id}",
                        User.Identity?.Name);
                    return NotFound();
                }

                await _appLogger.LogAsync("Information", "Products",
                    $"Opened Delete confirmation for product: '{product.Name}' (ID: {id})",
                    User.Identity?.Name);

                return View(product);
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error", "Products",
                    $"Error loading product for delete: {id} - {ex.Message}",
                    User.Identity?.Name);
                return NotFound();
            }
        }

        [HttpGet]
        public async Task<IActionResult> TestAzureStorage()
        {
            try
            {
                await _tableService.InitializeAsync();

                var products = await _tableService.GetAllProductsAsync();

                return Content(
                    $"Azure Table Storage connected successfully. " +
                    $"Products found: {products.Count}");
            }
            catch (Exception ex)
            {
                return Content(
                    "Azure Table Storage ERROR:\n\n" +
                    ex.ToString());
            }
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> DeleteConfirmed(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            try
            {
                var product = await _tableService.GetProductAsync(id);
                if (product != null)
                {
                    // Delete associated image
                    if (!string.IsNullOrEmpty(product.ImageUrl))
                    {
                        try
                        {
                            var uri = new Uri(product.ImageUrl);
                            var blobName = uri.Segments.Last();
                            await _blobService.DeleteImageAsync($"products/{blobName}");

                            await _appLogger.LogAsync("Information", "Products",
                                $"Image deleted for product '{product.Name}'",
                                User.Identity?.Name);
                        }
                        catch { }
                    }

                    await _tableService.DeleteProductAsync(id);

                    await _appLogger.LogAsync("Warning", "Products",
                        $"Product DELETED: '{product.Name}' | Price was: R{product.Price.ToString("F2")} | Stock was: {product.StockQuantity} | Category: {product.Category}",
                        User.Identity?.Name);

                    TempData["SuccessMessage"] = $"Product '{product.Name}' deleted successfully!";
                }
            }
            catch (Exception ex)
            {
                await _appLogger.LogAsync("Error", "Products",
                    $"Error deleting product {id}: {ex.Message}",
                    User.Identity?.Name);
                TempData["ErrorMessage"] = $"Error deleting product: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
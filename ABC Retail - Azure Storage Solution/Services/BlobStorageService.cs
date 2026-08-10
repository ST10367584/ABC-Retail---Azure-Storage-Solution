using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;

namespace ABCRetail.AzureStorage.Services
{
    public class BlobStorageService : IBlobStorageService
    {
        private readonly BlobServiceClient _blobServiceClient;
        private readonly IConfiguration _configuration;
        private BlobContainerClient? _containerClient;
        private readonly SemaphoreSlim _initLock = new SemaphoreSlim(1, 1);
        private bool _isInitialized = false;

        public BlobStorageService(BlobServiceClient blobServiceClient, IConfiguration configuration)
        {
            _blobServiceClient = blobServiceClient;
            _configuration = configuration;
        }

        public async Task InitializeAsync()
        {
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                var containerName = _configuration["AzureStorage:BlobContainer"] ?? "productimages";
                _containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                await _containerClient.CreateIfNotExistsAsync(PublicAccessType.Blob);
                _isInitialized = true;
                Console.WriteLine($"Blob container '{containerName}' initialized successfully.");
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

        public async Task<string> UploadImageAsync(IFormFile file, string? subFolder = null)
        {
            await EnsureInitializedAsync();

            if (_containerClient == null)
                throw new InvalidOperationException("Container not initialized");

            var extension = Path.GetExtension(file.FileName);
            var fileName = $"{Guid.NewGuid()}{extension}";

            var blobName = string.IsNullOrEmpty(subFolder)
                ? fileName
                : $"{subFolder}/{fileName}";

            var blobClient = _containerClient.GetBlobClient(blobName);

            using var stream = file.OpenReadStream();
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = file.ContentType });

            return blobName;
        }

        public async Task<Stream?> DownloadImageAsync(string blobName)
        {
            await EnsureInitializedAsync();

            if (_containerClient == null) return null;

            var blobClient = _containerClient.GetBlobClient(blobName);
            if (!await blobClient.ExistsAsync()) return null;

            var response = await blobClient.DownloadStreamingAsync();
            return response.Value.Content;
        }

        public async Task DeleteImageAsync(string blobName)
        {
            await EnsureInitializedAsync();

            if (_containerClient == null) return;

            var blobClient = _containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        public async Task<List<BlobInfo>> ListImagesAsync(string? prefix = null)
        {
            await EnsureInitializedAsync();

            var images = new List<BlobInfo>();
            if (_containerClient == null) return images;

            var blobItems = _containerClient.GetBlobsAsync(
                traits: BlobTraits.All,
                states: BlobStates.All,
                prefix: prefix,
                cancellationToken: CancellationToken.None
            );

            await foreach (var blobItem in blobItems)
            {
                var blobClient = _containerClient.GetBlobClient(blobItem.Name);
                images.Add(new BlobInfo
                {
                    Name = blobItem.Name,
                    Url = blobClient.Uri.ToString(),
                    Size = blobItem.Properties.ContentLength ?? 0,
                    LastModified = blobItem.Properties.LastModified?.DateTime,
                    ContentType = blobItem.Properties.ContentType
                });
            }
            return images;
        }

        public string GetImageUrl(string blobName)
        {
            // This doesn't need initialization as it just constructs a URL
            if (_containerClient == null) return string.Empty;
            var blobClient = _containerClient.GetBlobClient(blobName);
            return blobClient.Uri.ToString();
        }
    }
}
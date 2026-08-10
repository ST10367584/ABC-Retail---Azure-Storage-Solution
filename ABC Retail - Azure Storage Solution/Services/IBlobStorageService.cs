using Microsoft.AspNetCore.Http;

namespace ABCRetail.AzureStorage.Services
{
    public interface IBlobStorageService
    {
        Task InitializeAsync();
        Task<string> UploadImageAsync(IFormFile file, string? subFolder = null);
        Task<Stream?> DownloadImageAsync(string blobName);
        Task DeleteImageAsync(string blobName);
        Task<List<BlobInfo>> ListImagesAsync(string? prefix = null);
        string GetImageUrl(string blobName);
    }

    public class BlobInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public long Size { get; set; }
        public DateTime? LastModified { get; set; }
        public string? ContentType { get; set; }
    }
}
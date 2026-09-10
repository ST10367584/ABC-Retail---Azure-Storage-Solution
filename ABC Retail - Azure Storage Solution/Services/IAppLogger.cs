namespace ABCRetail.AzureStorage.Services
{
    public interface IAppLogger
    {
        Task LogAsync(string level, string source, string message, string? userId = null);
    }
}
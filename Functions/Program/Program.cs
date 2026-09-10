using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ABCRetail.Functions.Services;

namespace ABCRetail.Functions
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var host = new HostBuilder()
                .ConfigureFunctionsWebApplication()
                .ConfigureServices((context, services) =>
                {
                    var connectionString = context.Configuration["AzureStorageConnectionString"]
                        ?? Environment.GetEnvironmentVariable("AzureStorageConnectionString");

                    if (string.IsNullOrEmpty(connectionString))
                    {
                        throw new InvalidOperationException("AzureStorageConnectionString is not configured");
                    }

                    services.AddSingleton<IStorageService>(new StorageService(connectionString));
                })
                .Build();

            // Initialize storage services
            using (var scope = host.Services.CreateScope())
            {
                var storage = scope.ServiceProvider.GetRequiredService<IStorageService>();
                await storage.InitializeAsync();
            }

            await host.RunAsync();
        }
    }
}
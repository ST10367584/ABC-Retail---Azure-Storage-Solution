using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using ABCRetail.Functions.Services;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

// ✅ Register the storage service with connection string
var connectionString = Environment.GetEnvironmentVariable("AzureStorageConnectionString")
    ?? "DefaultEndpointsProtocol=https;AccountName=abcretailstoragegundo;AccountKey=uXzckZopFGCrm0tbjxjU2OH+k7sYmLFyLndHtua+cZaDmm4dtKPjxFp5iy1dEXmKC9G5kGcqDkQe+ASt08k+KQ==;EndpointSuffix=core.windows.net";

builder.Services.AddSingleton<IStorageService>(new StorageService(connectionString));

builder.Build().Run();
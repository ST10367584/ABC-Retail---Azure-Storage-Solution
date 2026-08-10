using ABCRetail.AzureStorage.Services;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// Get connection string from configuration (supports environment variables)
var connectionString = builder.Configuration.GetValue<string>("AzureStorage:ConnectionString");

// Log connection string status (remove in production)
Console.WriteLine($"Connection String loaded: {(string.IsNullOrEmpty(connectionString) ? "❌ NOT FOUND" : "✅ Found")}");

if (string.IsNullOrEmpty(connectionString))
{
    Console.WriteLine("WARNING: Connection string is missing! Please set AzureStorage__ConnectionString environment variable.");
}

// Register Azure Storage services
builder.Services.AddSingleton(x => new TableServiceClient(connectionString));
builder.Services.AddSingleton(x => new BlobServiceClient(connectionString));
builder.Services.AddSingleton(x => new QueueServiceClient(connectionString));
builder.Services.AddSingleton(x => new ShareServiceClient(connectionString));

// Register custom storage services
builder.Services.AddSingleton<ITableStorageService, TableStorageService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IQueueStorageService, QueueStorageService>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize Azure Storage resources
Console.WriteLine("Initializing Azure Storage resources...");
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var tableService = services.GetRequiredService<ITableStorageService>();
        var blobService = services.GetRequiredService<IBlobStorageService>();
        var queueService = services.GetRequiredService<IQueueStorageService>();
        var fileService = services.GetRequiredService<IFileStorageService>();

        await tableService.InitializeAsync();
        Console.WriteLine("✓ Table Storage initialized");

        await blobService.InitializeAsync();
        Console.WriteLine("✓ Blob Storage initialized");

        await queueService.InitializeAsync();
        Console.WriteLine("✓ Queue Storage initialized");

        await fileService.InitializeAsync();
        Console.WriteLine("✓ File Storage initialized");

        Console.WriteLine("All Azure Storage resources initialized successfully!");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error initializing Azure Storage: {ex.Message}");
        Console.WriteLine($"Stack Trace: {ex.StackTrace}");
    }
}

app.Run();
using ABCRetail.AzureStorage.Services;
using Azure.Data.Tables;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Files.Shares;
using Azure.Storage.Queues;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();

// ✅ Add Session Support (for cart)
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// ✅ Add Cookie Authentication
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.LogoutPath = "/Account/Logout";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

var connectionString = builder.Configuration.GetValue<string>("AzureStorage:ConnectionString");

builder.Services.AddSingleton(x => new TableServiceClient(connectionString));
builder.Services.AddSingleton(x => new BlobServiceClient(connectionString));
builder.Services.AddSingleton(x => new QueueServiceClient(connectionString));
builder.Services.AddSingleton(x => new ShareServiceClient(connectionString));

builder.Services.AddHttpContextAccessor();

builder.Services.AddSingleton<ITableStorageService, TableStorageService>();
builder.Services.AddSingleton<IBlobStorageService, BlobStorageService>();
builder.Services.AddSingleton<IQueueStorageService, QueueStorageService>();
builder.Services.AddSingleton<IFileStorageService, FileStorageService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IAppLogger, AppLogger>();
builder.Services.AddScoped<ICartService, CartService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// ✅ Order matters: Session → Authentication → Authorization
app.UseSession();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Initialize Azure Storage
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var tableService = services.GetRequiredService<ITableStorageService>();
        await tableService.InitializeAsync();

        var blobService = services.GetRequiredService<IBlobStorageService>();
        await blobService.InitializeAsync();

        var queueService = services.GetRequiredService<IQueueStorageService>();
        await queueService.InitializeAsync();

        var fileService = services.GetRequiredService<IFileStorageService>();
        await fileService.InitializeAsync();

        // ✅ Seed Default Admin User
        await SeedAdminUserAsync(services);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error initializing: {ex.Message}");
    }
}

app.Run();

// ✅ Seed Default Admin
static async Task SeedAdminUserAsync(IServiceProvider services)
{
    try
    {
        var auth = services.GetRequiredService<IAuthService>();

        var adminEmail = "admin@abcretail.com";
        if (!await auth.CustomerExistsAsync(adminEmail))
        {
            var admin = new ABCRetail.AzureStorage.Models.Customer
            {
                FirstName = "System",
                LastName = "Admin",
                Email = adminEmail,
                Password = "Admin@123",
                Role = "Admin",
                Phone = "0000000000",
                City = "System",
                Address = "System",
                PostalCode = "0000"
            };

            await auth.RegisterAsync(admin);
            Console.WriteLine("✅ Default admin created: admin@abcretail.com / Admin@123");
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error seeding admin: {ex.Message}");
    }
}
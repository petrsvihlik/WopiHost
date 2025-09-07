using WopiHost.AzureStorageProvider;
using WopiHost.Core.Extensions;
using WopiHost.Abstractions;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();

// Configure Azure Storage provider
builder.Services.Configure<WopiAzureStorageProviderOptions>(
    builder.Configuration.GetSection("WopiHost:StorageOptions"));

// Register Azure Storage services
builder.Services.AddSingleton<AzureFileIds>();
builder.Services.AddScoped<IWopiStorageProvider, WopiAzureStorageProvider>();
builder.Services.AddScoped<IWopiWritableStorageProvider, WopiAzureStorageProvider>();
builder.Services.AddScoped<IWopiSecurityHandler, WopiAzureSecurityHandler>();

// Add WOPI services
builder.Services.AddWopi();

// Add OpenAPI services
builder.Services.AddEndpointsApiExplorer();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    // OpenAPI endpoint will be available at /openapi/v1.json
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();

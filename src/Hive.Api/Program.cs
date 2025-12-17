using Hive.Api.Endpoints;
using Hive.Api.Extensions;
using Hive.Api.Services;
using Hive.Api.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new()
    {
        Title = "Document Management API",
        Version = "v1",
        Description = "API for managing documents with Azure CosmosDB, Blob Storage and Functions"
    });
});

// Add application services (CosmosDB, Blob Storage, etc.)
builder.Services.AddApplicationServices(builder.Configuration);

// Add CORS
builder.Services.AddCorsPolicy(builder.Configuration);

var app = builder.Build();

// Configure the HTTP request pipeline

// Global exception handling (must be first)
app.UseGlobalExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Document Management API v1");
        options.RoutePrefix = string.Empty; // Swagger UI at root URL
    });

    // Initialize CosmosDB on startup (only in development)
    using var scope = app.Services.CreateScope();
    var cosmosDbService = scope.ServiceProvider.GetRequiredService<ICosmosDbService>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    try
    {
        logger.LogInformation("Initializing CosmosDB database and containers...");
        await cosmosDbService.InitializeDatabaseAsync();
        logger.LogInformation("CosmosDB initialization completed successfully!");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Error initializing CosmosDB");
        // Don't interrupt application startup - emulator may not be running
    }
}

app.UseCors();

// Map endpoints
app.MapDocumentsEndpoints();
app.MapUploadEndpoints();
app.MapSearchEndpoints();
app.MapShareEndpoints();
app.MapVersionEndpoints();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new
{
    status = "healthy",
    timestamp = DateTime.UtcNow,
    environment = app.Environment.EnvironmentName
}))
.WithName("HealthCheck")
.WithTags("Health")
.WithOpenApi();

app.Run();

// Make Program class accessible for integration testing
public partial class Program { }

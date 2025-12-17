using Hive.Api.Configuration;
using Hive.Api.Services;

namespace Hive.Api.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddApplicationServices(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure options
        services.Configure<CosmosDbOptions>(
            configuration.GetSection(CosmosDbOptions.SectionName)
        );

        services.Configure<BlobStorageOptions>(
            configuration.GetSection(BlobStorageOptions.SectionName)
        );

        services.Configure<AzureQueueOptions>(
            configuration.GetSection(AzureQueueOptions.SectionName)
        );

        // Register services
        services.AddSingleton<ICosmosDbService, CosmosDbService>();
        services.AddSingleton<IBlobStorageService, BlobStorageService>();
        services.AddScoped<IChunkedUploadService, ChunkedUploadService>();
        services.AddScoped<IDocumentService, DocumentService>();
        services.AddScoped<IShareService, ShareService>();

        return services;
    }

    public static IServiceCollection AddCorsPolicy(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var allowedOrigins = configuration.GetSection("AllowedOrigins").Get<string[]>()
            ?? new[] { "http://localhost:5173", "http://localhost:3000" };

        services.AddCors(options =>
        {
            options.AddDefaultPolicy(builder =>
            {
                builder.WithOrigins(allowedOrigins)
                    .AllowAnyMethod()
                    .AllowAnyHeader()
                    .AllowCredentials();
            });
        });

        return services;
    }
}

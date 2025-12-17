using Azure.Storage.Blobs;
using Hive.Functions.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Azure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services =>
    {
        // Azure Cosmos DB Client
        var cosmosConnectionString = Environment.GetEnvironmentVariable("CosmosDBConnection")
            ?? throw new InvalidOperationException("CosmosDBConnection is not configured");

        services.AddSingleton(sp =>
        {
            return new CosmosClient(cosmosConnectionString);
        });

        // Azure Blob Storage Client
        var blobConnectionString = Environment.GetEnvironmentVariable("BlobStorageConnection")
            ?? Environment.GetEnvironmentVariable("AzureWebJobsStorage")
            ?? throw new InvalidOperationException("BlobStorageConnection or AzureWebJobsStorage is not configured");

        services.AddSingleton(sp =>
        {
            return new BlobServiceClient(blobConnectionString);
        });

        // Document Processing Services
        services.AddScoped<IOcrService, OcrService>();
        services.AddScoped<ITaggingService, TaggingService>();
        services.AddScoped<IThumbnailService, ThumbnailService>();
    })
    .Build();

host.Run();

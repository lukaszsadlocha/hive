using Azure.Storage.Blobs;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Hive.Functions.Models;
using Hive.Functions.Services;
using System.Text.Json;
using Microsoft.Azure.Cosmos;

namespace Hive.Functions.Functions;

public class DocumentProcessorFunction
{
    private readonly ILogger<DocumentProcessorFunction> _logger;
    private readonly IOcrService _ocrService;
    private readonly ITaggingService _taggingService;
    private readonly IThumbnailService _thumbnailService;
    private readonly BlobServiceClient _blobServiceClient;
    private readonly CosmosClient _cosmosClient;

    public DocumentProcessorFunction(
        ILogger<DocumentProcessorFunction> logger,
        IOcrService ocrService,
        ITaggingService taggingService,
        IThumbnailService thumbnailService,
        BlobServiceClient blobServiceClient,
        CosmosClient cosmosClient)
    {
        _logger = logger;
        _ocrService = ocrService;
        _taggingService = taggingService;
        _thumbnailService = thumbnailService;
        _blobServiceClient = blobServiceClient;
        _cosmosClient = cosmosClient;
    }

    [Function("DocumentProcessor")]
    public async Task Run(
        [QueueTrigger("document-processing-queue", Connection = "AzureWebJobsStorage")]
        string queueMessage)
    {
        var startTime = DateTime.UtcNow;

        try
        {
            _logger.LogInformation("Processing document from queue...");

            // Parse message
            var message = JsonSerializer.Deserialize<ProcessingMessage>(queueMessage);
            if (message == null)
            {
                _logger.LogError("Failed to deserialize queue message");
                return;
            }

            _logger.LogInformation($"Processing document: {message.DocumentId}, BlobPath: {message.BlobPath}");

            // Download blob for processing
            var blobClient = _blobServiceClient
                .GetBlobContainerClient("documents")
                .GetBlobClient(message.BlobPath);

            string? ocrText = null;
            List<string> autoTags = new();
            string? thumbnailPath = null;

            // Extract text using OCR if supported
            if (_ocrService.IsOcrSupported(message.ContentType))
            {
                _logger.LogInformation("Starting OCR extraction...");

                using var stream = new MemoryStream();
                await blobClient.DownloadToAsync(stream);
                stream.Position = 0;

                ocrText = await _ocrService.ExtractTextAsync(stream, message.ContentType);
                _logger.LogInformation($"OCR completed. Extracted {ocrText?.Length ?? 0} characters");
            }
            else
            {
                _logger.LogInformation("OCR not supported for this content type");
            }

            // Generate auto-tags
            _logger.LogInformation("Generating auto-tags...");
            var fileName = Path.GetFileName(message.BlobPath);
            autoTags = await _taggingService.GenerateTagsAsync(ocrText, fileName);
            _logger.LogInformation($"Generated {autoTags.Count} auto-tags");

            // Generate thumbnail if supported
            if (_thumbnailService.IsThumbnailSupported(message.ContentType))
            {
                _logger.LogInformation("Generating thumbnail...");

                using var stream = new MemoryStream();
                await blobClient.DownloadToAsync(stream);
                stream.Position = 0;

                thumbnailPath = await _thumbnailService.GenerateThumbnailAsync(
                    stream,
                    message.ContentType,
                    message.DocumentId);

                if (thumbnailPath != null)
                {
                    _logger.LogInformation($"Thumbnail generated: {thumbnailPath}");
                }
            }
            else
            {
                _logger.LogInformation("Thumbnail generation not supported for this content type");
            }

            // Calculate processing duration
            var processingDuration = (DateTime.UtcNow - startTime).TotalSeconds;

            // Update document in CosmosDB
            await UpdateDocumentAsync(
                message.DocumentId,
                message.UserId,
                ocrText,
                autoTags,
                thumbnailPath,
                processingDuration);

            _logger.LogInformation($"Document {message.DocumentId} processed successfully in {processingDuration:F2}s");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing document");

            // Try to mark document as failed
            try
            {
                var message = JsonSerializer.Deserialize<ProcessingMessage>(queueMessage);
                if (message != null)
                {
                    await MarkDocumentAsFailedAsync(message.DocumentId, message.UserId, ex.Message);
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to mark document as failed");
            }

            throw; // Re-throw to let Azure Functions handle retry logic
        }
    }

    private async Task UpdateDocumentAsync(
        string documentId,
        string userId,
        string? ocrText,
        List<string> autoTags,
        string? thumbnailPath,
        double processingDuration)
    {
        var container = _cosmosClient
            .GetDatabase("HiveDb")
            .GetContainer("documents");

        // Read current document
        var response = await container.ReadItemAsync<dynamic>(
            documentId,
            new PartitionKey(userId));

        var document = response.Resource;

        // Prepare update with all processed information
        var update = new DocumentUpdate
        {
            Id = documentId,
            UserId = userId,
            Status = "processed",
            Processing = new ProcessingInfo
            {
                OcrCompleted = ocrText != null,
                OcrText = ocrText,
                ThumbnailGenerated = thumbnailPath != null,
                ThumbnailPath = thumbnailPath,
                AutoTaggingCompleted = autoTags.Any(),
                ProcessedAt = DateTime.UtcNow,
                ProcessingDuration = processingDuration
            },
            Metadata = new DocumentMetadata
            {
                AutoTags = autoTags
            },
            Search = new SearchInfo
            {
                FullText = BuildFullTextSearchString(document, ocrText, autoTags)
            }
        };

        // Upsert updated document
        await container.UpsertItemAsync(update, new PartitionKey(userId));

        _logger.LogInformation($"Document {documentId} updated in CosmosDB");
    }

    private async Task MarkDocumentAsFailedAsync(string documentId, string userId, string errorMessage)
    {
        var container = _cosmosClient
            .GetDatabase("HiveDb")
            .GetContainer("documents");

        var update = new DocumentUpdate
        {
            Id = documentId,
            UserId = userId,
            Status = "failed",
            Processing = new ProcessingInfo
            {
                OcrCompleted = false,
                AutoTaggingCompleted = false,
                ProcessedAt = DateTime.UtcNow
            }
        };

        await container.UpsertItemAsync(update, new PartitionKey(userId));

        _logger.LogWarning($"Document {documentId} marked as failed: {errorMessage}");
    }

    private string BuildFullTextSearchString(dynamic document, string? ocrText, List<string> autoTags)
    {
        var parts = new List<string>();

        // Add document metadata
        if (document.metadata?.title != null)
            parts.Add(document.metadata.title.ToString());

        if (document.metadata?.description != null)
            parts.Add(document.metadata.description.ToString());

        if (document.metadata?.category != null)
            parts.Add(document.metadata.category.ToString());

        if (document.metadata?.tags != null)
        {
            foreach (var tag in document.metadata.tags)
            {
                parts.Add(tag.ToString());
            }
        }

        // Add auto-generated tags
        parts.AddRange(autoTags);

        // Add OCR text
        if (!string.IsNullOrEmpty(ocrText))
            parts.Add(ocrText);

        return string.Join(" ", parts);
    }
}

using Azure.Storage.Queues;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using Hive.Api.Configuration;
using Hive.Api.Models;

namespace Hive.Api.Services;

public class DocumentService : IDocumentService
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly QueueClient _queueClient;
    private readonly ILogger<DocumentService> _logger;

    public DocumentService(
        ICosmosDbService cosmosDbService,
        IBlobStorageService blobStorageService,
        IOptions<AzureQueueOptions> queueOptions,
        ILogger<DocumentService> logger)
    {
        _cosmosDbService = cosmosDbService;
        _blobStorageService = blobStorageService;
        _logger = logger;

        var options = queueOptions.Value;
        _queueClient = new QueueClient(options.ConnectionString, options.QueueName);
        _queueClient.CreateIfNotExists();
    }

    // ==================== CREATE DOCUMENT ====================

    public async Task<Document> CreateDocumentAsync(
        string fileName,
        string contentType,
        long fileSize,
        Stream fileStream,
        string userId)
    {
        try
        {
            _logger.LogInformation($"Creating document: {fileName}, Size: {fileSize} bytes");

            // STEP 1: Upload pliku do Blob Storage
            var blobPath = await _blobStorageService.UploadAsync(
                fileStream,
                fileName,
                contentType,
                "documents"
            );

            // STEP 2: Create document in CosmosDB
            var document = new Document
            {
                UserId = userId,
                FileName = fileName,
                ContentType = contentType,
                FileSize = fileSize,
                BlobPath = blobPath,
                BlobContainer = "documents",
                UploadedAt = DateTime.UtcNow,
                CurrentVersionId = "v1",
                Status = "uploaded",
                Metadata = new DocumentMetadata
                {
                    Title = Path.GetFileNameWithoutExtension(fileName)
                },
                Versions = new List<DocumentVersion>
                {
                    new DocumentVersion
                    {
                        VersionId = "v1",
                        BlobPath = blobPath,
                        FileSize = fileSize,
                        UploadedAt = DateTime.UtcNow,
                        UploadedBy = userId,
                        Comment = "Initial version"
                    }
                }
            };

            var createdDocument = await _cosmosDbService.CreateDocumentAsync(document);

            // STEP 3: Send message to queue for background processing
            await EnqueueProcessingMessageAsync(createdDocument);

            _logger.LogInformation($"Document created: {createdDocument.Id}");

            return createdDocument;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating document {fileName}");
            throw;
        }
    }

    // ==================== GET DOCUMENT ====================

    public async Task<Document?> GetDocumentAsync(string documentId, string userId)
    {
        try
        {
            return await _cosmosDbService.GetDocumentAsync(documentId, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting document {documentId}");
            throw;
        }
    }

    public async Task<Document> GetDocumentWithPreviewUrlAsync(string documentId, string userId)
    {
        try
        {
            var document = await _cosmosDbService.GetDocumentAsync(documentId, userId);

            if (document == null)
            {
                throw new InvalidOperationException($"Document {documentId} not found");
            }

            // Generate SAS token for preview (valid for 60 minutes)
            var previewUrl = await _blobStorageService.GenerateSasTokenAsync(document.BlobPath, 60);

            // You can add preview URL to metadata or return it another way
            // Na razie logujemy
            _logger.LogInformation($"Generated preview URL for document {documentId}");

            return document;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting document with preview URL {documentId}");
            throw;
        }
    }

    // ==================== UPDATE DOCUMENT ====================

    public async Task<Document> UpdateDocumentMetadataAsync(
        string documentId,
        string userId,
        DocumentMetadata metadata)
    {
        try
        {
            var document = await _cosmosDbService.GetDocumentAsync(documentId, userId);

            if (document == null)
            {
                throw new InvalidOperationException($"Document {documentId} not found");
            }

            document.Metadata = metadata;

            // Update search info
            document.Search = new SearchInfo
            {
                FullText = $"{document.FileName} {metadata.Title} {metadata.Description} {string.Join(" ", metadata.Tags)}".ToLower()
            };

            var updatedDocument = await _cosmosDbService.UpdateDocumentAsync(document);

            _logger.LogInformation($"Updated metadata for document {documentId}");

            return updatedDocument;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating document metadata {documentId}");
            throw;
        }
    }

    // ==================== DELETE DOCUMENT ====================

    public async Task DeleteDocumentAsync(string documentId, string userId)
    {
        try
        {
            _logger.LogInformation($"Deleting document {documentId}");

            // Get dokument
            var document = await _cosmosDbService.GetDocumentAsync(documentId, userId);

            if (document == null)
            {
                throw new InvalidOperationException($"Document {documentId} not found");
            }

            // Delete blob
            await _blobStorageService.DeleteAsync(document.BlobPath);

            // Delete wszystkie wersje
            foreach (var version in document.Versions)
            {
                await _blobStorageService.DeleteAsync(version.BlobPath);
            }

            // Delete thumbnail if exists
            if (!string.IsNullOrEmpty(document.Processing?.ThumbnailPath))
            {
                await _blobStorageService.DeleteAsync(document.Processing.ThumbnailPath);
            }

            // Delete dokument z CosmosDB
            await _cosmosDbService.DeleteDocumentAsync(documentId, userId);

            _logger.LogInformation($"Document {documentId} deleted successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting document {documentId}");
            throw;
        }
    }

    // ==================== QUERY AND SEARCH ====================

    public async Task<(List<Document> documents, string? continuationToken)> GetDocumentsAsync(
        string userId,
        string? category = null,
        string? sortBy = null,
        string? sortOrder = null,
        int pageSize = 20,
        string? continuationToken = null)
    {
        try
        {
            return await _cosmosDbService.QueryDocumentsAsync(
                userId,
                category,
                sortBy ?? "uploadedAt",
                sortOrder ?? "DESC",
                pageSize,
                continuationToken
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting documents");
            throw;
        }
    }

    public async Task<List<Document>> SearchDocumentsAsync(string searchText, string userId)
    {
        try
        {
            return await _cosmosDbService.SearchDocumentsAsync(searchText, userId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error searching documents with text: {searchText}");
            throw;
        }
    }

    // ==================== CHUNKED UPLOAD WORKFLOW ====================

    public async Task<string> CompleteChunkedUploadAsync(string sessionId)
    {
        try
        {
            _logger.LogInformation($"Processing chunked upload completion for session {sessionId}");

            // Get session
            var session = await _cosmosDbService.GetUploadSessionAsync(sessionId);

            if (session == null)
            {
                throw new InvalidOperationException($"Upload session {sessionId} not found");
            }

            // Chunks are already merged by ChunkedUploadService in blobPath

            // STEP 1: Create document in CosmosDB
            var document = new Document
            {
                UserId = session.UserId,
                FileName = session.FileName,
                ContentType = session.ContentType,
                FileSize = session.TotalSize,
                BlobPath = $"documents/{sessionId}", // This will be set after merge
                BlobContainer = "documents",
                UploadedAt = DateTime.UtcNow,
                CurrentVersionId = "v1",
                Status = "uploaded",
                Metadata = new DocumentMetadata
                {
                    Title = Path.GetFileNameWithoutExtension(session.FileName)
                },
                Versions = new List<DocumentVersion>
                {
                    new DocumentVersion
                    {
                        VersionId = "v1",
                        BlobPath = $"documents/{sessionId}",
                        FileSize = session.TotalSize,
                        UploadedAt = DateTime.UtcNow,
                        UploadedBy = session.UserId,
                        Comment = "Initial version (chunked upload)"
                    }
                }
            };

            var createdDocument = await _cosmosDbService.CreateDocumentAsync(document);

            // STEP 2: Send message to queue for processing
            await EnqueueProcessingMessageAsync(createdDocument);

            _logger.LogInformation($"Chunked upload completed: Document {createdDocument.Id} created");

            return createdDocument.Id;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error completing chunked upload for session {sessionId}");
            throw;
        }
    }

    // ==================== DOCUMENT VERSIONING ====================

    public async Task<DocumentVersion> CreateNewVersionAsync(
        string documentId,
        string userId,
        Stream fileStream,
        string? comment = null)
    {
        try
        {
            _logger.LogInformation($"Creating new version for document {documentId}");

            // Get existing document
            var document = await _cosmosDbService.GetDocumentAsync(documentId, userId);
            if (document == null)
            {
                throw new InvalidOperationException($"Document {documentId} not found");
            }

            // Upload new version to Blob Storage
            var versionNumber = document.Versions.Count + 1;
            var versionId = $"v{versionNumber}";
            var fileName = $"{Path.GetFileNameWithoutExtension(document.FileName)}_v{versionNumber}{Path.GetExtension(document.FileName)}";

            var blobPath = await _blobStorageService.UploadAsync(
                fileStream,
                fileName,
                document.ContentType,
                "documents"
            );

            // Create new version
            var newVersion = new DocumentVersion
            {
                VersionId = versionId,
                BlobPath = blobPath,
                FileSize = fileStream.Length,
                UploadedAt = DateTime.UtcNow,
                UploadedBy = userId,
                Comment = comment ?? $"Version {versionNumber}"
            };

            // Add to document versions list
            document.Versions.Add(newVersion);
            document.CurrentVersionId = versionId;

            // Update document in CosmosDB
            await _cosmosDbService.UpdateDocumentAsync(document);

            _logger.LogInformation($"Created version {versionId} for document {documentId}");

            return newVersion;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating new version for document {documentId}");
            throw;
        }
    }

    public async Task<List<DocumentVersion>> GetVersionsAsync(string documentId, string userId)
    {
        try
        {
            var document = await _cosmosDbService.GetDocumentAsync(documentId, userId);
            if (document == null)
            {
                throw new InvalidOperationException($"Document {documentId} not found");
            }

            return document.Versions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting versions for document {documentId}");
            throw;
        }
    }

    public async Task<DocumentVersion?> GetVersionAsync(
        string documentId,
        string userId,
        string versionId)
    {
        try
        {
            var document = await _cosmosDbService.GetDocumentAsync(documentId, userId);
            if (document == null)
            {
                throw new InvalidOperationException($"Document {documentId} not found");
            }

            var version = document.Versions.FirstOrDefault(v => v.VersionId == versionId);
            return version;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting version {versionId} for document {documentId}");
            throw;
        }
    }

    public async Task<Document> RestoreVersionAsync(
        string documentId,
        string userId,
        string versionId)
    {
        try
        {
            _logger.LogInformation($"Restoring document {documentId} to version {versionId}");

            var document = await _cosmosDbService.GetDocumentAsync(documentId, userId);
            if (document == null)
            {
                throw new InvalidOperationException($"Document {documentId} not found");
            }

            var versionToRestore = document.Versions.FirstOrDefault(v => v.VersionId == versionId);
            if (versionToRestore == null)
            {
                throw new InvalidOperationException($"Version {versionId} not found");
            }

            // Update current version and blob path
            document.CurrentVersionId = versionId;
            document.BlobPath = versionToRestore.BlobPath;
            document.FileSize = versionToRestore.FileSize;

            // Update in CosmosDB
            var updatedDocument = await _cosmosDbService.UpdateDocumentAsync(document);

            _logger.LogInformation($"Restored document {documentId} to version {versionId}");

            return updatedDocument;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error restoring document {documentId} to version {versionId}");
            throw;
        }
    }

    // ==================== HELPER METHODS ====================

    private async Task EnqueueProcessingMessageAsync(Document document)
    {
        try
        {
            var message = new ProcessingMessage
            {
                DocumentId = document.Id,
                UserId = document.UserId,
                BlobPath = document.BlobPath,
                ContentType = document.ContentType,
                EnqueuedAt = DateTime.UtcNow
            };

            var messageJson = JsonSerializer.Serialize(message);
            var messageBytes = Encoding.UTF8.GetBytes(messageJson);
            var base64Message = Convert.ToBase64String(messageBytes);

            await _queueClient.SendMessageAsync(base64Message);

            _logger.LogInformation($"Enqueued processing message for document {document.Id}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error enqueuing processing message for document {document.Id}");
            // Dont throw exception - document was created, queue is a bonus
        }
    }
}

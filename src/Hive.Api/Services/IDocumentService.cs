using Hive.Api.Models;

namespace Hive.Api.Services;

public interface IDocumentService
{
    // Document operations
    Task<Document> CreateDocumentAsync(string fileName, string contentType, long fileSize, Stream fileStream, string userId);
    Task<Document?> GetDocumentAsync(string documentId, string userId);
    Task<Document> GetDocumentWithPreviewUrlAsync(string documentId, string userId);
    Task<Document> UpdateDocumentMetadataAsync(string documentId, string userId, DocumentMetadata metadata);
    Task DeleteDocumentAsync(string documentId, string userId);

    // Query and search
    Task<(List<Document> documents, string? continuationToken)> GetDocumentsAsync(
        string userId,
        string? category = null,
        string? sortBy = null,
        string? sortOrder = null,
        int pageSize = 20,
        string? continuationToken = null);

    Task<List<Document>> SearchDocumentsAsync(string searchText, string userId);

    // Chunked upload workflow
    Task<string> CompleteChunkedUploadAsync(string sessionId);

    // Document versioning
    Task<DocumentVersion> CreateNewVersionAsync(string documentId, string userId, Stream fileStream, string? comment = null);
    Task<List<DocumentVersion>> GetVersionsAsync(string documentId, string userId);
    Task<DocumentVersion?> GetVersionAsync(string documentId, string userId, string versionId);
    Task<Document> RestoreVersionAsync(string documentId, string userId, string versionId);
}

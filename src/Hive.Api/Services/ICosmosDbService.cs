using Hive.Api.Models;

namespace Hive.Api.Services;

public interface ICosmosDbService
{
    // Initialization
    Task InitializeDatabaseAsync();

    // Document operations
    Task<Document> CreateDocumentAsync(Document document);
    Task<Document?> GetDocumentAsync(string documentId, string userId);
    Task<Document> UpdateDocumentAsync(Document document);
    Task DeleteDocumentAsync(string documentId, string userId);

    // Query operations
    Task<(List<Document> documents, string? continuationToken)> QueryDocumentsAsync(
        string userId,
        string? category = null,
        string? sortBy = "uploadedAt",
        string? sortOrder = "DESC",
        int pageSize = 20,
        string? continuationToken = null);

    // Search operations
    Task<List<Document>> SearchDocumentsAsync(string searchText, string userId);

    // Upload session operations
    Task<UploadSession> CreateUploadSessionAsync(UploadSession session);
    Task<UploadSession?> GetUploadSessionAsync(string sessionId);
    Task<UploadSession> UpdateUploadSessionAsync(UploadSession session);
    Task DeleteUploadSessionAsync(string sessionId);

    // Share link operations
    Task<ShareLink> CreateShareLinkAsync(ShareLink shareLink);
    Task<ShareLink?> GetShareLinkAsync(string linkId);
    Task<ShareLink> UpdateShareLinkAsync(ShareLink shareLink);
}

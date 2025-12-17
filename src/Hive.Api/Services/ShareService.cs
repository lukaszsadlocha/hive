using Hive.Api.Models;
using System.Security.Cryptography;

namespace Hive.Api.Services;

public interface IShareService
{
    Task<ShareLink> CreateShareLinkAsync(
        string documentId,
        string userId,
        int? expiresInHours = null,
        int? maxAccessCount = null,
        string? password = null,
        List<string>? permissions = null);

    Task<(Document? document, string? error)> GetSharedDocumentAsync(string token, string? password = null);

    Task RevokeShareLinkAsync(string linkId, string userId);

    Task<List<ShareLink>> GetUserShareLinksAsync(string userId);
}

public class ShareService : IShareService
{
    private readonly ICosmosDbService _cosmosDb;
    private readonly IBlobStorageService _blobStorage;
    private readonly ILogger<ShareService> _logger;

    public ShareService(
        ICosmosDbService cosmosDb,
        IBlobStorageService blobStorage,
        ILogger<ShareService> logger)
    {
        _cosmosDb = cosmosDb;
        _blobStorage = blobStorage;
        _logger = logger;
    }

    public async Task<ShareLink> CreateShareLinkAsync(
        string documentId,
        string userId,
        int? expiresInHours = null,
        int? maxAccessCount = null,
        string? password = null,
        List<string>? permissions = null)
    {
        _logger.LogInformation($"Creating share link for document {documentId}");

        // Verify document exists and belongs to user
        var document = await _cosmosDb.GetDocumentAsync(documentId, userId);
        if (document == null)
        {
            throw new InvalidOperationException($"Document {documentId} not found or access denied");
        }

        // Generate secure random token
        var token = GenerateSecureToken();
        var linkId = Guid.NewGuid().ToString();

        var shareLink = new ShareLink
        {
            Id = linkId,
            LinkId = linkId,
            DocumentId = documentId,
            UserId = userId,
            Token = token,
            ExpiresAt = expiresInHours.HasValue
                ? DateTime.UtcNow.AddHours(expiresInHours.Value)
                : null,
            MaxAccessCount = maxAccessCount,
            Password = password != null ? HashPassword(password) : null,
            Permissions = permissions ?? new List<string> { "view", "download" },
            CreatedAt = DateTime.UtcNow,
            AccessCount = 0
        };

        // Calculate TTL if expiration is set
        if (shareLink.ExpiresAt.HasValue)
        {
            var ttlSeconds = (int)(shareLink.ExpiresAt.Value - DateTime.UtcNow).TotalSeconds;
            shareLink.Ttl = ttlSeconds > 0 ? ttlSeconds : 1;
        }

        var created = await _cosmosDb.CreateShareLinkAsync(shareLink);
        _logger.LogInformation($"Created share link {linkId} with token");

        return created;
    }

    public async Task<(Document? document, string? error)> GetSharedDocumentAsync(
        string token,
        string? password = null)
    {
        _logger.LogInformation($"Accessing shared document with token");

        // Find share link by token
        var shareLink = await FindShareLinkByTokenAsync(token);
        if (shareLink == null)
        {
            return (null, "Invalid or expired share link");
        }

        // Validate share link
        var validationError = ValidateShareLink(shareLink, password);
        if (validationError != null)
        {
            return (null, validationError);
        }

        // Get the document
        var document = await _cosmosDb.GetDocumentAsync(shareLink.DocumentId, shareLink.UserId);
        if (document == null)
        {
            return (null, "Document not found");
        }

        // Increment access count
        await IncrementAccessCountAsync(shareLink);

        _logger.LogInformation(
            $"Document {document.Id} accessed via share link " +
            $"(access count: {shareLink.AccessCount + 1})"
        );

        return (document, null);
    }

    public async Task RevokeShareLinkAsync(string linkId, string userId)
    {
        _logger.LogInformation($"Revoking share link {linkId}");

        // Get share link to verify ownership
        var shareLink = await _cosmosDb.GetShareLinkAsync(linkId);
        if (shareLink == null)
        {
            throw new InvalidOperationException($"Share link {linkId} not found");
        }

        if (shareLink.UserId != userId)
        {
            throw new UnauthorizedAccessException("You don't have permission to revoke this link");
        }

        // Delete from CosmosDB (no delete method in interface, need to update CosmosDbService)
        // For now, we'll update it with immediate expiration
        shareLink.ExpiresAt = DateTime.UtcNow.AddSeconds(-1);
        shareLink.Ttl = 1;
        await _cosmosDb.UpdateShareLinkAsync(shareLink);

        _logger.LogInformation($"Revoked share link {linkId}");
    }

    public Task<List<ShareLink>> GetUserShareLinksAsync(string userId)
    {
        // This would require a new query method in CosmosDbService
        // For now, return empty list - to be implemented
        _logger.LogWarning("GetUserShareLinksAsync not yet implemented - requires CosmosDB query");
        return Task.FromResult(new List<ShareLink>());
    }

    // ==================== PRIVATE HELPERS ====================

    private Task<ShareLink?> FindShareLinkByTokenAsync(string token)
    {
        // Note: This is inefficient - we should add an index on token or use a different approach
        // For now, this is a placeholder implementation
        // In production, consider using token as partition key or adding a secondary index

        // We can't efficiently query by token without a proper index
        // This would require adding a query method to CosmosDbService
        // For demonstration, we'll return null and log a warning

        _logger.LogWarning(
            "FindShareLinkByTokenAsync requires cross-partition query - " +
            "consider redesigning with token as partition key or using secondary index"
        );

        // TODO: Implement proper token lookup
        // Option 1: Store token → linkId mapping in a separate container
        // Option 2: Use token as partition key instead of linkId
        // Option 3: Use CosmosDB change feed to maintain a token index

        return Task.FromResult<ShareLink?>(null);
    }

    private string? ValidateShareLink(ShareLink shareLink, string? password)
    {
        // Check expiration
        if (shareLink.ExpiresAt.HasValue && shareLink.ExpiresAt.Value <= DateTime.UtcNow)
        {
            return "Share link has expired";
        }

        // Check access count limit
        if (shareLink.MaxAccessCount.HasValue && shareLink.AccessCount >= shareLink.MaxAccessCount.Value)
        {
            return "Share link has reached maximum access count";
        }

        // Check password
        if (!string.IsNullOrEmpty(shareLink.Password))
        {
            if (string.IsNullOrEmpty(password))
            {
                return "Password required";
            }

            if (!VerifyPassword(password, shareLink.Password))
            {
                return "Invalid password";
            }
        }

        return null;
    }

    private async Task IncrementAccessCountAsync(ShareLink shareLink)
    {
        shareLink.AccessCount++;
        await _cosmosDb.UpdateShareLinkAsync(shareLink);
    }

    private static string GenerateSecureToken()
    {
        // Generate 32-byte random token and encode as base64url
        var bytes = new byte[32];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);

        // Base64url encoding (URL-safe)
        return Convert.ToBase64String(bytes)
            .Replace('+', '-')
            .Replace('/', '_')
            .TrimEnd('=');
    }

    private static string HashPassword(string password)
    {
        // Simple SHA256 hash for demonstration
        // In production, use proper password hashing like BCrypt or Argon2
        using var sha256 = SHA256.Create();
        var bytes = System.Text.Encoding.UTF8.GetBytes(password);
        var hash = sha256.ComputeHash(bytes);
        return Convert.ToBase64String(hash);
    }

    private static bool VerifyPassword(string password, string hashedPassword)
    {
        var hash = HashPassword(password);
        return hash == hashedPassword;
    }
}

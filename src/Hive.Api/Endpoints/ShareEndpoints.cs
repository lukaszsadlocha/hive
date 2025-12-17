using Hive.Api.Models;
using Hive.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hive.Api.Endpoints;

public static class ShareEndpoints
{
    public static void MapShareEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/share")
            .WithTags("Share")
            .WithOpenApi();

        // POST /api/share - Create share link
        group.MapPost("/", CreateShareLink)
            .WithName("CreateShareLink")
            .WithSummary("Create a share link for a document")
            .Produces<ShareLinkResponse>(201)
            .Produces(404)
            .Produces(400);

        // GET /api/share/{token} - Access shared document
        group.MapGet("/{token}", GetSharedDocument)
            .WithName("GetSharedDocument")
            .WithSummary("Access a document via share token")
            .Produces<SharedDocumentResponse>()
            .Produces(404)
            .Produces(401)
            .Produces(403);

        // DELETE /api/share/{linkId} - Revoke share link
        group.MapDelete("/{linkId}", RevokeShareLink)
            .WithName("RevokeShareLink")
            .WithSummary("Revoke a share link")
            .Produces(204)
            .Produces(404)
            .Produces(403);

        // GET /api/share - List user's share links
        group.MapGet("/", GetUserShareLinks)
            .WithName("GetUserShareLinks")
            .WithSummary("Get all share links created by user")
            .Produces<List<ShareLink>>();
    }

    // ==================== HANDLERS ====================

    private static async Task<IResult> CreateShareLink(
        [FromQuery] string userId,
        [FromQuery] string documentId,
        [FromBody] CreateShareLinkRequest request,
        IShareService shareService,
        ILogger<IShareService> logger)
    {
        try
        {
            logger.LogInformation($"Creating share link for document {documentId}");

            var shareLink = await shareService.CreateShareLinkAsync(
                documentId,
                userId,
                request.ExpiresInHours,
                request.MaxAccessCount,
                request.Password,
                request.Permissions
            );

            var response = new ShareLinkResponse
            {
                LinkId = shareLink.LinkId,
                Token = shareLink.Token,
                ShareUrl = $"/api/share/{shareLink.Token}",
                ExpiresAt = shareLink.ExpiresAt,
                MaxAccessCount = shareLink.MaxAccessCount,
                Permissions = shareLink.Permissions,
                CreatedAt = shareLink.CreatedAt
            };

            return Results.Created($"/api/share/{shareLink.LinkId}", response);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Invalid operation while creating share link");
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating share link");
            return Results.BadRequest(new { error = "Failed to create share link" });
        }
    }

    private static async Task<IResult> GetSharedDocument(
        string token,
        [FromQuery] string? password,
        IShareService shareService,
        IBlobStorageService blobStorage,
        ILogger<IShareService> logger)
    {
        try
        {
            logger.LogInformation($"Accessing shared document");

            var (document, error) = await shareService.GetSharedDocumentAsync(token, password);

            if (document == null || error != null)
            {
                logger.LogWarning($"Access denied: {error}");

                // Return appropriate status code based on error
                if (error?.Contains("password", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Results.Unauthorized();
                }
                if (error?.Contains("expired", StringComparison.OrdinalIgnoreCase) == true ||
                    error?.Contains("maximum", StringComparison.OrdinalIgnoreCase) == true)
                {
                    return Results.StatusCode(403); // Forbidden
                }

                return Results.NotFound(new { error });
            }

            // Generate preview URL with SAS token
            var previewUrl = await blobStorage.GenerateSasTokenAsync(
                document.BlobPath,
                expiryMinutes: 60
            );

            var response = new SharedDocumentResponse
            {
                Document = document,
                PreviewUrl = previewUrl
            };

            return Results.Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error accessing shared document");
            return Results.BadRequest(new { error = "Failed to access shared document" });
        }
    }

    private static async Task<IResult> RevokeShareLink(
        string linkId,
        [FromQuery] string userId,
        IShareService shareService,
        ILogger<IShareService> logger)
    {
        try
        {
            logger.LogInformation($"Revoking share link {linkId}");

            await shareService.RevokeShareLinkAsync(linkId, userId);

            return Results.NoContent();
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Share link not found");
            return Results.NotFound(new { error = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            logger.LogWarning(ex, "Unauthorized revoke attempt");
            return Results.StatusCode(403); // Forbidden
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error revoking share link");
            return Results.BadRequest(new { error = "Failed to revoke share link" });
        }
    }

    private static async Task<IResult> GetUserShareLinks(
        [FromQuery] string userId,
        IShareService shareService,
        ILogger<IShareService> logger)
    {
        try
        {
            logger.LogInformation($"Getting share links for user {userId}");

            var shareLinks = await shareService.GetUserShareLinksAsync(userId);

            return Results.Ok(shareLinks);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting user share links");
            return Results.BadRequest(new { error = "Failed to get share links" });
        }
    }
}

// ==================== REQUEST/RESPONSE MODELS ====================

public class CreateShareLinkRequest
{
    public int? ExpiresInHours { get; set; }
    public int? MaxAccessCount { get; set; }
    public string? Password { get; set; }
    public List<string>? Permissions { get; set; }
}

public class ShareLinkResponse
{
    public string LinkId { get; set; } = string.Empty;
    public string Token { get; set; } = string.Empty;
    public string ShareUrl { get; set; } = string.Empty;
    public DateTime? ExpiresAt { get; set; }
    public int? MaxAccessCount { get; set; }
    public List<string> Permissions { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

public class SharedDocumentResponse
{
    public Document Document { get; set; } = new();
    public string PreviewUrl { get; set; } = string.Empty;
}

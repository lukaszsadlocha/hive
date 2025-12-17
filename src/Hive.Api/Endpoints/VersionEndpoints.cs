using Hive.Api.Models;
using Hive.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hive.Api.Endpoints;

public static class VersionEndpoints
{
    public static void MapVersionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents/{documentId}/versions")
            .WithTags("Versions")
            .WithOpenApi();

        // POST /api/documents/{id}/versions - Create new version
        group.MapPost("/", CreateNewVersion)
            .WithName("CreateNewVersion")
            .WithSummary("Create a new version of a document")
            .Produces<DocumentVersion>(201)
            .Produces(404)
            .Produces(400)
            .DisableAntiforgery();

        // GET /api/documents/{id}/versions - List all versions
        group.MapGet("/", GetVersions)
            .WithName("GetVersions")
            .WithSummary("Get all versions of a document")
            .Produces<List<DocumentVersion>>()
            .Produces(404);

        // GET /api/documents/{id}/versions/{versionId} - Get specific version
        group.MapGet("/{versionId}", GetVersion)
            .WithName("GetVersion")
            .WithSummary("Get a specific version of a document")
            .Produces<DocumentVersion>()
            .Produces(404);

        // POST /api/documents/{id}/versions/{versionId}/restore - Restore version
        group.MapPost("/{versionId}/restore", RestoreVersion)
            .WithName("RestoreVersion")
            .WithSummary("Restore document to a specific version")
            .Produces<Document>()
            .Produces(404)
            .Produces(400);

        // GET /api/documents/{id}/versions/{versionId}/preview - Preview version
        group.MapGet("/{versionId}/preview", GetVersionPreview)
            .WithName("GetVersionPreview")
            .WithSummary("Get preview URL for a specific version")
            .Produces<VersionPreviewResponse>()
            .Produces(404);
    }

    // ==================== HANDLERS ====================

    private static async Task<IResult> CreateNewVersion(
        string documentId,
        [FromQuery] string userId,
        IFormFile file,
        [FromForm] string? comment,
        IDocumentService documentService,
        ILogger<IDocumentService> logger)
    {
        try
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(new { error = "File is required" });
            }

            logger.LogInformation($"Creating new version for document {documentId}");

            using var stream = file.OpenReadStream();
            var newVersion = await documentService.CreateNewVersionAsync(
                documentId,
                userId,
                stream,
                comment
            );

            return Results.Created(
                $"/api/documents/{documentId}/versions/{newVersion.VersionId}",
                newVersion
            );
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Document not found");
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating new version");
            return Results.BadRequest(new { error = "Failed to create new version" });
        }
    }

    private static async Task<IResult> GetVersions(
        string documentId,
        [FromQuery] string userId,
        IDocumentService documentService,
        ILogger<IDocumentService> logger)
    {
        try
        {
            logger.LogInformation($"Getting versions for document {documentId}");

            var versions = await documentService.GetVersionsAsync(documentId, userId);

            return Results.Ok(versions);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Document not found");
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting versions");
            return Results.BadRequest(new { error = "Failed to get versions" });
        }
    }

    private static async Task<IResult> GetVersion(
        string documentId,
        string versionId,
        [FromQuery] string userId,
        IDocumentService documentService,
        ILogger<IDocumentService> logger)
    {
        try
        {
            logger.LogInformation($"Getting version {versionId} for document {documentId}");

            var version = await documentService.GetVersionAsync(documentId, userId, versionId);

            if (version == null)
            {
                return Results.NotFound(new { error = $"Version {versionId} not found" });
            }

            return Results.Ok(version);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Document not found");
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting version");
            return Results.BadRequest(new { error = "Failed to get version" });
        }
    }

    private static async Task<IResult> RestoreVersion(
        string documentId,
        string versionId,
        [FromQuery] string userId,
        IDocumentService documentService,
        ILogger<IDocumentService> logger)
    {
        try
        {
            logger.LogInformation($"Restoring document {documentId} to version {versionId}");

            var restoredDocument = await documentService.RestoreVersionAsync(
                documentId,
                userId,
                versionId
            );

            return Results.Ok(restoredDocument);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Document or version not found");
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error restoring version");
            return Results.BadRequest(new { error = "Failed to restore version" });
        }
    }

    private static async Task<IResult> GetVersionPreview(
        string documentId,
        string versionId,
        [FromQuery] string userId,
        IDocumentService documentService,
        IBlobStorageService blobStorage,
        ILogger<IDocumentService> logger)
    {
        try
        {
            logger.LogInformation($"Getting preview URL for version {versionId} of document {documentId}");

            var version = await documentService.GetVersionAsync(documentId, userId, versionId);

            if (version == null)
            {
                return Results.NotFound(new { error = $"Version {versionId} not found" });
            }

            // Generate SAS token for preview
            var previewUrl = await blobStorage.GenerateSasTokenAsync(
                version.BlobPath,
                expiryMinutes: 60
            );

            var response = new VersionPreviewResponse
            {
                Version = version,
                PreviewUrl = previewUrl
            };

            return Results.Ok(response);
        }
        catch (InvalidOperationException ex)
        {
            logger.LogWarning(ex, "Document not found");
            return Results.NotFound(new { error = ex.Message });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error getting version preview");
            return Results.BadRequest(new { error = "Failed to get version preview" });
        }
    }
}

// ==================== RESPONSE MODELS ====================

public class VersionPreviewResponse
{
    public DocumentVersion Version { get; set; } = new();
    public string PreviewUrl { get; set; } = string.Empty;
}

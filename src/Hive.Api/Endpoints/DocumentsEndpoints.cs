using Hive.Api.Models;
using Hive.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hive.Api.Endpoints;

public static class DocumentsEndpoints
{
    public static void MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents")
            .WithTags("Documents")
            .WithOpenApi();

        // GET /api/documents - List of documents with filtering and sorting
        group.MapGet("/", GetDocuments)
            .WithName("GetDocuments")
            .WithSummary("Get list of documents")
            .Produces<GetDocumentsResponse>();

        // GET /api/documents/{id} - Single document
        group.MapGet("/{id}", GetDocument)
            .WithName("GetDocument")
            .WithSummary("Get document by ID")
            .Produces<Document>()
            .Produces(404);

        // GET /api/documents/{id}/preview - Document preview URL
        group.MapGet("/{id}/preview", GetDocumentPreview)
            .WithName("GetDocumentPreview")
            .WithSummary("Get document preview URL with SAS token")
            .Produces<PreviewUrlResponse>();

        // POST /api/documents - Simple upload (for small files)
        group.MapPost("/", CreateDocument)
            .WithName("CreateDocument")
            .WithSummary("Upload a document (for small files)")
            .Produces<Document>(201)
            .DisableAntiforgery();

        // PUT /api/documents/{id} - Update metadata
        group.MapPut("/{id}", UpdateDocument)
            .WithName("UpdateDocument")
            .WithSummary("Update document metadata")
            .Produces<Document>();

        // DELETE /api/documents/{id} - Delete document
        group.MapDelete("/{id}", DeleteDocument)
            .WithName("DeleteDocument")
            .WithSummary("Delete document")
            .Produces(204)
            .Produces(404);
    }

    // ==================== HANDLERS ====================

    private static async Task<IResult> GetDocuments(
        [FromServices] IDocumentService documentService,
        [FromQuery] string? category = null,
        [FromQuery] string? sortBy = "uploadedAt",
        [FromQuery] string? sortOrder = "DESC",
        [FromQuery] int pageSize = 20,
        [FromQuery] string? continuationToken = null,
        [FromQuery] string userId = "default-user")
    {
        var (documents, nextToken) = await documentService.GetDocumentsAsync(
            userId,
            category,
            sortBy,
            sortOrder,
            pageSize,
            continuationToken
        );

        return Results.Ok(new GetDocumentsResponse
        {
            Documents = documents,
            ContinuationToken = nextToken,
            Count = documents.Count
        });
    }

    private static async Task<IResult> GetDocument(
        string id,
        [FromServices] IDocumentService documentService,
        [FromQuery] string userId = "default-user")
    {
        var document = await documentService.GetDocumentAsync(id, userId);

        if (document == null)
        {
            return Results.NotFound(new { message = $"Document {id} not found" });
        }

        return Results.Ok(document);
    }

    private static async Task<IResult> GetDocumentPreview(
        string id,
        [FromServices] IDocumentService documentService,
        [FromServices] IBlobStorageService blobStorageService,
        [FromQuery] string userId = "default-user")
    {
        var document = await documentService.GetDocumentAsync(id, userId);

        if (document == null)
        {
            return Results.NotFound(new { message = $"Document {id} not found" });
        }

        // Generate SAS URL
        var previewUrl = await blobStorageService.GenerateSasTokenAsync(
            document.BlobPath,
            expiryMinutes: 60
        );

        return Results.Ok(new PreviewUrlResponse
        {
            Url = previewUrl,
            ExpiresAt = DateTime.UtcNow.AddMinutes(60)
        });
    }

    private static async Task<IResult> CreateDocument(
        HttpContext context,
        [FromServices] IDocumentService documentService,
        [FromQuery] string userId = "default-user")
    {
        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Request must be multipart/form-data" });
        }

        var form = await context.Request.ReadFormAsync();
        var file = form.Files.GetFile("file");

        if (file == null || file.Length == 0)
        {
            return Results.BadRequest(new { message = "No file uploaded" });
        }

        // Check size (max 100MB for simple upload)
        if (file.Length > 100 * 1024 * 1024)
        {
            return Results.BadRequest(new
            {
                message = "File too large. Use chunked upload for files larger than 100MB"
            });
        }

        using var stream = file.OpenReadStream();

        var document = await documentService.CreateDocumentAsync(
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            file.Length,
            stream,
            userId
        );

        return Results.Created($"/api/documents/{document.Id}", document);
    }

    private static async Task<IResult> UpdateDocument(
        string id,
        [FromBody] UpdateDocumentRequest request,
        [FromServices] IDocumentService documentService,
        [FromQuery] string userId = "default-user")
    {
        var document = await documentService.UpdateDocumentMetadataAsync(
            id,
            userId,
            request.Metadata
        );

        return Results.Ok(document);
    }

    private static async Task<IResult> DeleteDocument(
        string id,
        [FromServices] IDocumentService documentService,
        [FromQuery] string userId = "default-user")
    {
        await documentService.DeleteDocumentAsync(id, userId);
        return Results.NoContent();
    }
}

// ==================== DTOs ====================

public record GetDocumentsResponse
{
    public List<Document> Documents { get; init; } = new();
    public string? ContinuationToken { get; init; }
    public int Count { get; init; }
}

public record PreviewUrlResponse
{
    public string Url { get; init; } = string.Empty;
    public DateTime ExpiresAt { get; init; }
}

public record UpdateDocumentRequest
{
    public DocumentMetadata Metadata { get; init; } = new();
}

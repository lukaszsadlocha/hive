using Hive.Api.Models;
using Hive.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hive.Api.Endpoints;

public static class UploadEndpoints
{
    public static void MapUploadEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents/upload")
            .WithTags("Upload")
            .WithOpenApi();

        // POST /api/documents/upload/init - Initialize upload session
        group.MapPost("/init", InitializeUpload)
            .WithName("InitializeUpload")
            .WithSummary("Initialize chunked upload session")
            .Produces<UploadSession>(201);

        // POST /api/documents/upload/chunk - Upload a single chunk
        group.MapPost("/chunk", UploadChunk)
            .WithName("UploadChunk")
            .WithSummary("Upload a single chunk")
            .Produces<UploadSession>()
            .DisableAntiforgery();

        // POST /api/documents/upload/complete - Finalize upload
        group.MapPost("/complete", CompleteUpload)
            .WithName("CompleteUpload")
            .WithSummary("Complete chunked upload")
            .Produces<CompleteUploadResponse>(201);

        // GET /api/documents/upload/{sessionId}/progress - Upload progress
        group.MapGet("/{sessionId}/progress", GetUploadProgress)
            .WithName("GetUploadProgress")
            .WithSummary("Get upload progress")
            .Produces<UploadProgressResponse>();
    }

    // ==================== HANDLERS ====================

    private static async Task<IResult> InitializeUpload(
        [FromBody] InitializeUploadRequest request,
        [FromServices] IChunkedUploadService chunkedUploadService,
        [FromQuery] string userId = "default-user")
    {
        var session = await chunkedUploadService.InitializeUploadSessionAsync(
            request.FileName,
            request.ContentType,
            request.TotalSize,
            request.TotalChunks,
            userId
        );

        return Results.Created($"/api/documents/upload/{session.SessionId}/progress", session);
    }

    private static async Task<IResult> UploadChunk(
        HttpContext context,
        [FromServices] IChunkedUploadService chunkedUploadService)
    {
        if (!context.Request.HasFormContentType)
        {
            return Results.BadRequest(new { message = "Request must be multipart/form-data" });
        }

        var form = await context.Request.ReadFormAsync();

        // Get parameters
        if (!form.TryGetValue("sessionId", out var sessionIdValue) ||
            !form.TryGetValue("chunkIndex", out var chunkIndexValue))
        {
            return Results.BadRequest(new { message = "Missing sessionId or chunkIndex" });
        }

        var sessionId = sessionIdValue.ToString();
        var chunkIndex = int.Parse(chunkIndexValue.ToString());

        // Get chunk file
        var chunk = form.Files.GetFile("chunk");

        if (chunk == null || chunk.Length == 0)
        {
            return Results.BadRequest(new { message = "No chunk data provided" });
        }

        using var stream = chunk.OpenReadStream();

        var session = await chunkedUploadService.UploadChunkAsync(
            sessionId,
            chunkIndex,
            stream
        );

        return Results.Ok(session);
    }

    private static async Task<IResult> CompleteUpload(
        [FromBody] CompleteUploadRequest request,
        [FromServices] IChunkedUploadService chunkedUploadService,
        [FromServices] IDocumentService documentService)
    {
        // STEP 1: Merge chunks
        var blobPath = await chunkedUploadService.CompleteUploadAsync(request.SessionId);

        // STEP 2: Create document
        var documentId = await documentService.CompleteChunkedUploadAsync(request.SessionId);

        return Results.Created($"/api/documents/{documentId}", new CompleteUploadResponse
        {
            DocumentId = documentId,
            BlobPath = blobPath,
            Message = "Upload completed successfully"
        });
    }

    private static async Task<IResult> GetUploadProgress(
        string sessionId,
        [FromServices] IChunkedUploadService chunkedUploadService)
    {
        var session = await chunkedUploadService.GetUploadProgressAsync(sessionId);

        if (session == null)
        {
            return Results.NotFound(new { message = $"Upload session {sessionId} not found" });
        }

        var progress = session.TotalChunks > 0
            ? (double)session.UploadedChunks.Count / session.TotalChunks * 100
            : 0;

        return Results.Ok(new UploadProgressResponse
        {
            SessionId = session.SessionId,
            FileName = session.FileName,
            TotalChunks = session.TotalChunks,
            UploadedChunks = session.UploadedChunks.Count,
            Progress = progress,
            Status = session.Status
        });
    }
}

// ==================== DTOs ====================

public record InitializeUploadRequest
{
    public string FileName { get; init; } = string.Empty;
    public string ContentType { get; init; } = "application/octet-stream";
    public long TotalSize { get; init; }
    public int TotalChunks { get; init; }
}

public record CompleteUploadRequest
{
    public string SessionId { get; init; } = string.Empty;
}

public record CompleteUploadResponse
{
    public string DocumentId { get; init; } = string.Empty;
    public string BlobPath { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public record UploadProgressResponse
{
    public string SessionId { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public int TotalChunks { get; init; }
    public int UploadedChunks { get; init; }
    public double Progress { get; init; }
    public string Status { get; init; } = string.Empty;
}

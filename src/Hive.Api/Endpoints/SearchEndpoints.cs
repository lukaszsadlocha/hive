using Hive.Api.Models;
using Hive.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Hive.Api.Endpoints;

public static class SearchEndpoints
{
    public static void MapSearchEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/search")
            .WithTags("Search")
            .WithOpenApi();

        // POST /api/search - Full-text search
        group.MapPost("/", SearchDocuments)
            .WithName("SearchDocuments")
            .WithSummary("Search documents by text")
            .Produces<SearchResponse>();
    }

    // ==================== HANDLERS ====================

    private static async Task<IResult> SearchDocuments(
        [FromBody] SearchRequest request,
        [FromServices] IDocumentService documentService,
        [FromQuery] string userId = "default-user")
    {
        if (string.IsNullOrWhiteSpace(request.SearchText))
        {
            return Results.BadRequest(new { message = "Search text cannot be empty" });
        }

        var documents = await documentService.SearchDocumentsAsync(request.SearchText, userId);

        return Results.Ok(new SearchResponse
        {
            Documents = documents,
            Count = documents.Count,
            SearchText = request.SearchText
        });
    }
}

// ==================== DTOs ====================

public record SearchRequest
{
    public string SearchText { get; init; } = string.Empty;
}

public record SearchResponse
{
    public List<Document> Documents { get; init; } = new();
    public int Count { get; init; }
    public string SearchText { get; init; } = string.Empty;
}

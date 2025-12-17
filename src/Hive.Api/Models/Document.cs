using System.Text.Json.Serialization;

namespace Hive.Api.Models;

public class Document
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("type")]
    public string Type { get; set; } = "document";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "default-user"; // Partition key

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("fileSize")]
    public long FileSize { get; set; }

    [JsonPropertyName("blobPath")]
    public string BlobPath { get; set; } = string.Empty;

    [JsonPropertyName("blobContainer")]
    public string BlobContainer { get; set; } = "documents";

    [JsonPropertyName("uploadedAt")]
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("currentVersionId")]
    public string CurrentVersionId { get; set; } = "v1";

    [JsonPropertyName("status")]
    public string Status { get; set; } = "uploaded"; // uploaded, processing, processed, failed

    [JsonPropertyName("metadata")]
    public DocumentMetadata Metadata { get; set; } = new();

    [JsonPropertyName("processing")]
    public ProcessingInfo? Processing { get; set; }

    [JsonPropertyName("versions")]
    public List<DocumentVersion> Versions { get; set; } = new();

    [JsonPropertyName("search")]
    public SearchInfo? Search { get; set; }

    [JsonPropertyName("_ts")]
    public long? Timestamp { get; set; } // CosmosDB timestamp
}

public class DocumentMetadata
{
    [JsonPropertyName("title")]
    public string? Title { get; set; }

    [JsonPropertyName("description")]
    public string? Description { get; set; }

    [JsonPropertyName("category")]
    public string? Category { get; set; }

    [JsonPropertyName("tags")]
    public List<string> Tags { get; set; } = new();

    [JsonPropertyName("autoTags")]
    public List<string> AutoTags { get; set; } = new();

    [JsonPropertyName("customFields")]
    public Dictionary<string, string> CustomFields { get; set; } = new();
}

public class ProcessingInfo
{
    [JsonPropertyName("ocrCompleted")]
    public bool OcrCompleted { get; set; }

    [JsonPropertyName("ocrText")]
    public string? OcrText { get; set; }

    [JsonPropertyName("thumbnailGenerated")]
    public bool ThumbnailGenerated { get; set; }

    [JsonPropertyName("thumbnailPath")]
    public string? ThumbnailPath { get; set; }

    [JsonPropertyName("autoTaggingCompleted")]
    public bool AutoTaggingCompleted { get; set; }

    [JsonPropertyName("processedAt")]
    public DateTime? ProcessedAt { get; set; }

    [JsonPropertyName("processingDuration")]
    public double? ProcessingDuration { get; set; }
}

public class SearchInfo
{
    [JsonPropertyName("fullText")]
    public string FullText { get; set; } = string.Empty;

    [JsonPropertyName("searchableFields")]
    public List<string> SearchableFields { get; set; } = new();
}

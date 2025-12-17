using System.Text.Json.Serialization;

namespace Hive.Functions.Models;

public class DocumentUpdate
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    [JsonPropertyName("status")]
    public string Status { get; set; } = string.Empty;

    [JsonPropertyName("processing")]
    public ProcessingInfo? Processing { get; set; }

    [JsonPropertyName("metadata")]
    public DocumentMetadata? Metadata { get; set; }

    [JsonPropertyName("search")]
    public SearchInfo? Search { get; set; }
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

public class DocumentMetadata
{
    [JsonPropertyName("autoTags")]
    public List<string> AutoTags { get; set; } = new();
}

public class SearchInfo
{
    [JsonPropertyName("fullText")]
    public string FullText { get; set; } = string.Empty;
}

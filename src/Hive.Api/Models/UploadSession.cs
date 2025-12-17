using System.Text.Json.Serialization;

namespace Hive.Api.Models;

public class UploadSession
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty; // Partition key

    [JsonPropertyName("type")]
    public string Type { get; set; } = "upload-session";

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "default-user";

    [JsonPropertyName("fileName")]
    public string FileName { get; set; } = string.Empty;

    [JsonPropertyName("contentType")]
    public string ContentType { get; set; } = string.Empty;

    [JsonPropertyName("totalSize")]
    public long TotalSize { get; set; }

    [JsonPropertyName("totalChunks")]
    public int TotalChunks { get; set; }

    [JsonPropertyName("chunkSize")]
    public int ChunkSize { get; set; } = 5 * 1024 * 1024; // 5MB default

    [JsonPropertyName("uploadedChunks")]
    public List<int> UploadedChunks { get; set; } = new();

    [JsonPropertyName("status")]
    public string Status { get; set; } = "in-progress"; // in-progress, completed, failed

    [JsonPropertyName("tempBlobContainer")]
    public string TempBlobContainer { get; set; } = "upload-temp";

    [JsonPropertyName("tempBlobPrefix")]
    public string TempBlobPrefix { get; set; } = string.Empty;

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("lastUpdatedAt")]
    public DateTime LastUpdatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("expiresAt")]
    public DateTime ExpiresAt { get; set; } = DateTime.UtcNow.AddHours(24);

    [JsonPropertyName("ttl")]
    public int Ttl { get; set; } = 86400; // 24 hours in seconds
}

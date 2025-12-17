using System.Text.Json.Serialization;

namespace Hive.Api.Models;

public class ShareLink
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString();

    [JsonPropertyName("linkId")]
    public string LinkId { get; set; } = string.Empty; // Partition key

    [JsonPropertyName("type")]
    public string Type { get; set; } = "share-link";

    [JsonPropertyName("documentId")]
    public string DocumentId { get; set; } = string.Empty;

    [JsonPropertyName("userId")]
    public string UserId { get; set; } = "default-user";

    [JsonPropertyName("token")]
    public string Token { get; set; } = string.Empty;

    [JsonPropertyName("expiresAt")]
    public DateTime? ExpiresAt { get; set; }

    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [JsonPropertyName("accessCount")]
    public int AccessCount { get; set; } = 0;

    [JsonPropertyName("maxAccessCount")]
    public int? MaxAccessCount { get; set; }

    [JsonPropertyName("password")]
    public string? Password { get; set; }

    [JsonPropertyName("permissions")]
    public List<string> Permissions { get; set; } = new() { "view", "download" };

    [JsonPropertyName("ttl")]
    public int? Ttl { get; set; } // TTL in seconds
}

namespace Hive.Functions.Models;

public class ProcessingMessage
{
    public string DocumentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string BlobPath { get; set; } = string.Empty;
    public string ContentType { get; set; } = string.Empty;
    public DateTime EnqueuedAt { get; set; }
}

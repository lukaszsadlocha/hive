namespace Hive.Functions.Services;

public interface IThumbnailService
{
    Task<string?> GenerateThumbnailAsync(Stream fileStream, string contentType, string documentId);
    bool IsThumbnailSupported(string contentType);
}

namespace Hive.Api.Services;

public interface IBlobStorageService
{
    // Basic operations
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string containerName);
    Task<Stream> DownloadAsync(string blobPath);
    Task DeleteAsync(string blobPath);
    Task<bool> ExistsAsync(string blobPath);

    // SAS token for preview
    Task<string> GenerateSasTokenAsync(string blobPath, int expiryMinutes = 60);

    // Chunked upload operations
    Task<string> UploadChunkAsync(Stream chunkStream, string sessionId, int chunkIndex, string containerName);
    Task<string> MergeChunksAsync(string sessionId, int totalChunks, string finalFileName, string containerName);
    Task CleanupTempChunksAsync(string sessionId, string containerName);

    // Container operations
    Task EnsureContainerExistsAsync(string containerName);
}

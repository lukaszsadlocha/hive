using Hive.Api.Models;

namespace Hive.Api.Services;

public interface IChunkedUploadService
{
    Task<UploadSession> InitializeUploadSessionAsync(
        string fileName,
        string contentType,
        long totalSize,
        int totalChunks,
        string userId);

    Task<UploadSession> UploadChunkAsync(
        string sessionId,
        int chunkIndex,
        Stream chunkStream);

    Task<string> CompleteUploadAsync(string sessionId);

    Task<UploadSession?> GetUploadProgressAsync(string sessionId);

    Task CleanupFailedUploadAsync(string sessionId);
}

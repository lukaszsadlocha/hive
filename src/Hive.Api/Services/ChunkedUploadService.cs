using Hive.Api.Models;

namespace Hive.Api.Services;

public class ChunkedUploadService : IChunkedUploadService
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ILogger<ChunkedUploadService> _logger;

    public ChunkedUploadService(
        ICosmosDbService cosmosDbService,
        IBlobStorageService blobStorageService,
        ILogger<ChunkedUploadService> logger)
    {
        _cosmosDbService = cosmosDbService;
        _blobStorageService = blobStorageService;
        _logger = logger;
    }

    // ==================== INITIALIZE UPLOAD SESSION ====================

    public async Task<UploadSession> InitializeUploadSessionAsync(
        string fileName,
        string contentType,
        long totalSize,
        int totalChunks,
        string userId)
    {
        try
        {
            _logger.LogInformation(
                $"Initializing upload session: {fileName}, Size: {totalSize} bytes, Chunks: {totalChunks}"
            );

            var sessionId = Guid.NewGuid().ToString("N");

            var uploadSession = new UploadSession
            {
                Id = sessionId,
                SessionId = sessionId,
                UserId = userId,
                FileName = fileName,
                ContentType = contentType,
                TotalSize = totalSize,
                TotalChunks = totalChunks,
                ChunkSize = 5 * 1024 * 1024, // 5MB
                Status = "in-progress",
                TempBlobContainer = "upload-temp",
                TempBlobPrefix = sessionId,
                CreatedAt = DateTime.UtcNow,
                LastUpdatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24),
                Ttl = 86400 // 24 hours
            };

            // Save session in CosmosDB
            var createdSession = await _cosmosDbService.CreateUploadSessionAsync(uploadSession);

            // Ensure temp container exists
            await _blobStorageService.EnsureContainerExistsAsync("upload-temp");

            _logger.LogInformation($"Upload session created: {sessionId}");

            return createdSession;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing upload session");
            throw;
        }
    }

    // ==================== UPLOAD CHUNK ====================

    public async Task<UploadSession> UploadChunkAsync(
        string sessionId,
        int chunkIndex,
        Stream chunkStream)
    {
        try
        {
            _logger.LogInformation($"Uploading chunk {chunkIndex} for session {sessionId}");

            // Get session
            var session = await _cosmosDbService.GetUploadSessionAsync(sessionId);

            if (session == null)
            {
                throw new InvalidOperationException($"Upload session {sessionId} not found");
            }

            if (session.Status != "in-progress")
            {
                throw new InvalidOperationException(
                    $"Upload session {sessionId} is not in progress (status: {session.Status})"
                );
            }

            // Check if chunk was not already uploaded
            if (session.UploadedChunks.Contains(chunkIndex))
            {
                _logger.LogWarning($"Chunk {chunkIndex} already uploaded for session {sessionId}");
                return session;
            }

            // Upload chunka do Blob Storage
            await _blobStorageService.UploadChunkAsync(
                chunkStream,
                sessionId,
                chunkIndex,
                "upload-temp"
            );

            // Update session
            session.UploadedChunks.Add(chunkIndex);
            session.UploadedChunks.Sort(); // Sort for better readability
            session.LastUpdatedAt = DateTime.UtcNow;

            var updatedSession = await _cosmosDbService.UpdateUploadSessionAsync(session);

            _logger.LogInformation(
                $"Chunk {chunkIndex} uploaded. Progress: {session.UploadedChunks.Count}/{session.TotalChunks}"
            );

            return updatedSession;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error uploading chunk {chunkIndex} for session {sessionId}");
            throw;
        }
    }

    // ==================== COMPLETE UPLOAD ====================

    public async Task<string> CompleteUploadAsync(string sessionId)
    {
        try
        {
            _logger.LogInformation($"Completing upload for session {sessionId}");

            // Get session
            var session = await _cosmosDbService.GetUploadSessionAsync(sessionId);

            if (session == null)
            {
                throw new InvalidOperationException($"Upload session {sessionId} not found");
            }

            // Check if all chunks were uploaded
            if (session.UploadedChunks.Count != session.TotalChunks)
            {
                throw new InvalidOperationException(
                    $"Not all chunks uploaded. Expected: {session.TotalChunks}, Got: {session.UploadedChunks.Count}"
                );
            }

            // Check if all indices are present (0 to TotalChunks-1)
            for (int i = 0; i < session.TotalChunks; i++)
            {
                if (!session.UploadedChunks.Contains(i))
                {
                    throw new InvalidOperationException($"Missing chunk {i}");
                }
            }

            // Scal chunki w finalny plik
            _logger.LogInformation($"Merging {session.TotalChunks} chunks...");

            var finalBlobPath = await _blobStorageService.MergeChunksAsync(
                sessionId,
                session.TotalChunks,
                session.FileName,
                "documents" // Target container
            );

            _logger.LogInformation($"Chunks merged successfully: {finalBlobPath}");

            // Cleanup temp chunks
            await _blobStorageService.CleanupTempChunksAsync(sessionId, "upload-temp");

            // Update status sesji
            session.Status = "completed";
            session.LastUpdatedAt = DateTime.UtcNow;
            await _cosmosDbService.UpdateUploadSessionAsync(session);

            _logger.LogInformation($"Upload completed for session {sessionId}: {finalBlobPath}");

            return finalBlobPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error completing upload for session {sessionId}");

            // Mark session as failed
            try
            {
                var session = await _cosmosDbService.GetUploadSessionAsync(sessionId);
                if (session != null)
                {
                    session.Status = "failed";
                    await _cosmosDbService.UpdateUploadSessionAsync(session);
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Error updating session status to failed");
            }

            throw;
        }
    }

    // ==================== GET UPLOAD PROGRESS ====================

    public async Task<UploadSession?> GetUploadProgressAsync(string sessionId)
    {
        try
        {
            var session = await _cosmosDbService.GetUploadSessionAsync(sessionId);

            if (session != null)
            {
                _logger.LogInformation(
                    $"Upload progress for session {sessionId}: {session.UploadedChunks.Count}/{session.TotalChunks} chunks"
                );
            }

            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting upload progress for session {sessionId}");
            throw;
        }
    }

    // ==================== CLEANUP FAILED UPLOAD ====================

    public async Task CleanupFailedUploadAsync(string sessionId)
    {
        try
        {
            _logger.LogInformation($"Cleaning up failed upload for session {sessionId}");

            // Cleanup temp chunks z Blob Storage
            await _blobStorageService.CleanupTempChunksAsync(sessionId, "upload-temp");

            // Delete session from CosmosDB
            await _cosmosDbService.DeleteUploadSessionAsync(sessionId);

            _logger.LogInformation($"Cleanup completed for session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error cleaning up failed upload for session {sessionId}");
            // Dont throw exception - cleanup shouldnt block
        }
    }
}

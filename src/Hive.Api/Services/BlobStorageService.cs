using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Options;
using Hive.Api.Configuration;

namespace Hive.Api.Services;

public class BlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _blobServiceClient;
    private readonly ILogger<BlobStorageService> _logger;
    private readonly BlobStorageOptions _options;

    public BlobStorageService(
        IOptions<BlobStorageOptions> options,
        ILogger<BlobStorageService> logger)
    {
        _options = options.Value;
        _logger = logger;
        _blobServiceClient = new BlobServiceClient(_options.ConnectionString);
    }

    // ==================== BASIC OPERATIONS ====================

    public async Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, string containerName)
    {
        try
        {
            await EnsureContainerExistsAsync(containerName);

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            // Generate unique blob path
            var blobName = GenerateBlobName(fileName);
            var blobClient = containerClient.GetBlobClient(blobName);

            // Upload with metadata
            var uploadOptions = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    ContentType = contentType
                },
                Metadata = new Dictionary<string, string>
                {
                    { "OriginalFileName", fileName },
                    { "UploadedAt", DateTime.UtcNow.ToString("o") }
                }
            };

            fileStream.Position = 0;
            await blobClient.UploadAsync(fileStream, uploadOptions);

            var blobPath = $"{containerName}/{blobName}";
            _logger.LogInformation($"Uploaded blob: {blobPath}");

            return blobPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error uploading file {fileName} to container {containerName}");
            throw;
        }
    }

    public async Task<Stream> DownloadAsync(string blobPath)
    {
        try
        {
            var (containerName, blobName) = ParseBlobPath(blobPath);
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            var response = await blobClient.DownloadStreamingAsync();
            _logger.LogInformation($"Downloaded blob: {blobPath}");

            return response.Value.Content;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error downloading blob {blobPath}");
            throw;
        }
    }

    public async Task DeleteAsync(string blobPath)
    {
        try
        {
            var (containerName, blobName) = ParseBlobPath(blobPath);
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            await blobClient.DeleteIfExistsAsync();
            _logger.LogInformation($"Deleted blob: {blobPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error deleting blob {blobPath}");
            throw;
        }
    }

    public async Task<bool> ExistsAsync(string blobPath)
    {
        try
        {
            var (containerName, blobName) = ParseBlobPath(blobPath);
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            return await blobClient.ExistsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking blob existence {blobPath}");
            return false;
        }
    }

    // ==================== SAS TOKEN ====================

    public async Task<string> GenerateSasTokenAsync(string blobPath, int expiryMinutes = 60)
    {
        try
        {
            var (containerName, blobName) = ParseBlobPath(blobPath);
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            // Check if blob exists
            if (!await blobClient.ExistsAsync())
            {
                throw new FileNotFoundException($"Blob not found: {blobPath}");
            }

            // Generate SAS token
            var sasBuilder = new BlobSasBuilder
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b", // b = blob
                StartsOn = DateTimeOffset.UtcNow.AddMinutes(-5), // Add buffer
                ExpiresOn = DateTimeOffset.UtcNow.AddMinutes(expiryMinutes)
            };

            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasToken = blobClient.GenerateSasUri(sasBuilder).ToString();
            _logger.LogInformation($"Generated SAS token for blob: {blobPath}, expires in {expiryMinutes} minutes");

            return sasToken;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating SAS token for blob {blobPath}");
            throw;
        }
    }

    // ==================== CHUNKED UPLOAD ====================

    public async Task<string> UploadChunkAsync(Stream chunkStream, string sessionId, int chunkIndex, string containerName)
    {
        try
        {
            await EnsureContainerExistsAsync(containerName);

            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            // Nazwa chunka: sessionId/chunk-0, sessionId/chunk-1, etc.
            var chunkBlobName = $"{sessionId}/chunk-{chunkIndex}";
            var blobClient = containerClient.GetBlobClient(chunkBlobName);

            chunkStream.Position = 0;
            await blobClient.UploadAsync(chunkStream, overwrite: true);

            var chunkPath = $"{containerName}/{chunkBlobName}";
            _logger.LogInformation($"Uploaded chunk: {chunkPath}");

            return chunkPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error uploading chunk {chunkIndex} for session {sessionId}");
            throw;
        }
    }

    public async Task<string> MergeChunksAsync(string sessionId, int totalChunks, string finalFileName, string containerName)
    {
        try
        {
            var tempContainerClient = _blobServiceClient.GetBlobContainerClient(_options.ContainerNames.UploadTemp);

            // Target container
            await EnsureContainerExistsAsync(containerName);
            var targetContainerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            // Generate final blob name
            var finalBlobName = GenerateBlobName(finalFileName);
            var finalBlobClient = targetContainerClient.GetBlobClient(finalBlobName);

            _logger.LogInformation($"Merging {totalChunks} chunks for session {sessionId}");

            // Get all chunks and merge them
            using var mergedStream = new MemoryStream();

            for (int i = 0; i < totalChunks; i++)
            {
                var chunkBlobName = $"{sessionId}/chunk-{i}";
                var chunkBlobClient = tempContainerClient.GetBlobClient(chunkBlobName);

                if (!await chunkBlobClient.ExistsAsync())
                {
                    throw new InvalidOperationException($"Chunk {i} not found for session {sessionId}");
                }

                var chunkStream = await chunkBlobClient.DownloadStreamingAsync();
                await chunkStream.Value.Content.CopyToAsync(mergedStream);

                _logger.LogInformation($"Merged chunk {i}/{totalChunks}");
            }

            // Upload scalony plik
            mergedStream.Position = 0;
            await finalBlobClient.UploadAsync(mergedStream, overwrite: true);

            var finalBlobPath = $"{containerName}/{finalBlobName}";
            _logger.LogInformation($"Successfully merged chunks into: {finalBlobPath}");

            return finalBlobPath;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error merging chunks for session {sessionId}");
            throw;
        }
    }

    public async Task CleanupTempChunksAsync(string sessionId, string containerName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);

            // Delete all blobs with sessionId prefix
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: sessionId))
            {
                var blobClient = containerClient.GetBlobClient(blobItem.Name);
                await blobClient.DeleteIfExistsAsync();
                _logger.LogInformation($"Deleted temp chunk: {blobItem.Name}");
            }

            _logger.LogInformation($"Cleaned up temp chunks for session {sessionId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error cleaning up temp chunks for session {sessionId}");
            // Don't throw exception - cleanup shouldn't block the main process
        }
    }

    // ==================== CONTAINER OPERATIONS ====================

    public async Task EnsureContainerExistsAsync(string containerName)
    {
        try
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync(PublicAccessType.None);
            _logger.LogInformation($"Container '{containerName}' is ready");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error ensuring container {containerName} exists");
            throw;
        }
    }

    // ==================== HELPER METHODS ====================

    private string GenerateBlobName(string fileName)
    {
        // Generate unique name with date and GUID
        var timestamp = DateTime.UtcNow.ToString("yyyy/MM/dd");
        var guid = Guid.NewGuid().ToString("N").Substring(0, 8);
        var extension = Path.GetExtension(fileName);
        var fileNameWithoutExt = Path.GetFileNameWithoutExtension(fileName);

        // Sanitize filename
        var sanitizedFileName = SanitizeFileName(fileNameWithoutExt);

        return $"{timestamp}/{sanitizedFileName}-{guid}{extension}";
    }

    private string SanitizeFileName(string fileName)
    {
        // Remove dangerous characters
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = new string(fileName.Where(c => !invalid.Contains(c)).ToArray());

        // Limit length
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }

        return sanitized;
    }

    private (string containerName, string blobName) ParseBlobPath(string blobPath)
    {
        // blobPath format: "containerName/path/to/blob.ext"
        var parts = blobPath.Split('/', 2);

        if (parts.Length != 2)
        {
            throw new ArgumentException($"Invalid blob path format: {blobPath}");
        }

        return (parts[0], parts[1]);
    }
}

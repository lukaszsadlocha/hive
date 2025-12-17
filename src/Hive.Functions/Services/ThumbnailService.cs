using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using SixLabors.ImageSharp.Formats.Jpeg;

namespace Hive.Functions.Services;

public class ThumbnailService : IThumbnailService
{
    private readonly ILogger<ThumbnailService> _logger;
    private readonly BlobServiceClient _blobServiceClient;

    public ThumbnailService(
        ILogger<ThumbnailService> logger,
        BlobServiceClient blobServiceClient)
    {
        _logger = logger;
        _blobServiceClient = blobServiceClient;
    }

    public async Task<string?> GenerateThumbnailAsync(Stream fileStream, string contentType, string documentId)
    {
        if (!IsThumbnailSupported(contentType))
        {
            _logger.LogInformation($"Thumbnail generation not supported for content type: {contentType}");
            return null;
        }

        _logger.LogInformation($"Generating thumbnail for document {documentId} with ImageSharp...");

        try
        {
            const int thumbnailWidth = 300;
            const int thumbnailHeight = 300;

            // For images, use ImageSharp
            if (contentType.StartsWith("image/"))
            {
                return await GenerateImageThumbnailAsync(fileStream, documentId, thumbnailWidth, thumbnailHeight);
            }

            // For PDF, we would need a PDF library like Docnet.Core
            // For now, return null for PDFs (can be added later)
            if (contentType == "application/pdf")
            {
                _logger.LogInformation($"PDF thumbnail generation not yet implemented for {documentId}");
                return null;
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error generating thumbnail for document {documentId}");
            return null;
        }
    }

    private async Task<string> GenerateImageThumbnailAsync(Stream fileStream, string documentId, int width, int height)
    {
        using var image = await Image.LoadAsync(fileStream);

        _logger.LogInformation($"Original image size: {image.Width}x{image.Height}");

        // Resize proportionally
        image.Mutate(x => x.Resize(new ResizeOptions
        {
            Size = new Size(width, height),
            Mode = ResizeMode.Max // Maintains aspect ratio
        }));

        _logger.LogInformation($"Resized thumbnail to: {image.Width}x{image.Height}");

        // Save to MemoryStream as JPEG
        using var thumbnailStream = new MemoryStream();
        var encoder = new JpegEncoder { Quality = 85 }; // Good quality/size balance
        await image.SaveAsJpegAsync(thumbnailStream, encoder);
        thumbnailStream.Position = 0;

        _logger.LogInformation($"Thumbnail size: {thumbnailStream.Length} bytes");

        // Upload to blob storage
        var thumbnailPath = $"thumbnails/{documentId}_thumb.jpg";
        var containerClient = _blobServiceClient.GetBlobContainerClient("thumbnails");
        await containerClient.CreateIfNotExistsAsync();

        var blobClient = containerClient.GetBlobClient(thumbnailPath);
        await blobClient.UploadAsync(thumbnailStream, overwrite: true);

        _logger.LogInformation($"Thumbnail successfully generated and saved to: {thumbnailPath}");

        return thumbnailPath;
    }

    public bool IsThumbnailSupported(string contentType)
    {
        return contentType switch
        {
            "application/pdf" => true,
            "image/jpeg" => true,
            "image/jpg" => true,
            "image/png" => true,
            "image/gif" => true,
            "image/bmp" => true,
            "image/tiff" => true,
            "image/webp" => true,
            _ => false
        };
    }
}

/*
 * PRODUKCYJNA IMPLEMENTACJA Z IMAGESHARP:
 *
 * using SixLabors.ImageSharp;
 * using SixLabors.ImageSharp.Processing;
 *
 * public async Task<string?> GenerateThumbnailAsync(Stream fileStream, string contentType, string documentId)
 * {
 *     const int thumbnailWidth = 300;
 *     const int thumbnailHeight = 300;
 *
 *     using var image = await Image.LoadAsync(fileStream);
 *
 *     // Resize proporcjonalnie
 *     image.Mutate(x => x.Resize(new ResizeOptions
 *     {
 *         Size = new Size(thumbnailWidth, thumbnailHeight),
 *         Mode = ResizeMode.Max
 *     }));
 *
 *     // Save do MemoryStream
 *     using var thumbnailStream = new MemoryStream();
 *     await image.SaveAsJpegAsync(thumbnailStream);
 *     thumbnailStream.Position = 0;
 *
 *     // Upload do blob storage
 *     var thumbnailPath = $"thumbnails/{documentId}_thumb.jpg";
 *     var containerClient = _blobServiceClient.GetBlobContainerClient("thumbnails");
 *     await containerClient.CreateIfNotExistsAsync();
 *
 *     var blobClient = containerClient.GetBlobClient(thumbnailPath);
 *     await blobClient.UploadAsync(thumbnailStream, overwrite: true);
 *
 *     return thumbnailPath;
 * }
 */

/*
 * PRODUKCYJNA IMPLEMENTACJA DLA PDF Z DOCNET.CORE:
 *
 * using Docnet.Core;
 * using Docnet.Core.Models;
 *
 * if (contentType == "application/pdf")
 * {
 *     using var docReader = DocLib.Instance.GetDocReader(fileStream, new PageDimensions(1080, 1920));
 *     using var pageReader = docReader.GetPageReader(0); // Pierwsza strona
 *
 *     var rawBytes = pageReader.GetImage();
 *     var width = pageReader.GetPageWidth();
 *     var height = pageReader.GetPageHeight();
 *
 *     // Konwertuj na obraz i resize
 *     using var image = Image.LoadPixelData<Bgra32>(rawBytes, width, height);
 *     image.Mutate(x => x.Resize(300, 300));
 *
 *     // Upload do blob storage...
 * }
 */

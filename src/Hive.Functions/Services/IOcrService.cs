namespace Hive.Functions.Services;

public interface IOcrService
{
    Task<string?> ExtractTextAsync(Stream fileStream, string contentType);
    bool IsOcrSupported(string contentType);
}

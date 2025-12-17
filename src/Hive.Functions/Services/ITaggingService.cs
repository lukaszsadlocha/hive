namespace Hive.Functions.Services;

public interface ITaggingService
{
    Task<List<string>> GenerateTagsAsync(string? content, string fileName);
}

namespace Hive.Api.Configuration;

public class AzureQueueOptions
{
    public const string SectionName = "AzureQueue";

    public string ConnectionString { get; set; } = string.Empty;
    public string QueueName { get; set; } = "document-processing-queue";
}

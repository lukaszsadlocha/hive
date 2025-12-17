namespace Hive.Api.Configuration;

public class CosmosDbOptions
{
    public const string SectionName = "CosmosDb";

    public string Endpoint { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string DatabaseName { get; set; } = "HiveDb";
    public bool EnableLocalEmulator { get; set; } = false;
    public ContainerNames ContainerNames { get; set; } = new();
}

public class ContainerNames
{
    public string Documents { get; set; } = "documents";
    public string UploadSessions { get; set; } = "upload-sessions";
    public string ShareLinks { get; set; } = "share-links";
}

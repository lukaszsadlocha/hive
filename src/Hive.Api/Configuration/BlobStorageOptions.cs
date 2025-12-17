namespace Hive.Api.Configuration;

public class BlobStorageOptions
{
    public const string SectionName = "BlobStorage";

    public string ConnectionString { get; set; } = string.Empty;
    public BlobContainerNames ContainerNames { get; set; } = new();
}

public class BlobContainerNames
{
    public string Documents { get; set; } = "documents";
    public string Thumbnails { get; set; } = "thumbnails";
    public string UploadTemp { get; set; } = "upload-temp";
}

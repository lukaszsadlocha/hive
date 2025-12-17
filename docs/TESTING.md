# Testing Guide

Comprehensive testing strategy for the Hive Document Management System.

## Testing Pyramid

```
        ┌─────────────┐
        │   E2E Tests │  (5%)
        │             │  Selenium, Playwright
        └─────────────┘
      ┌───────────────────┐
      │ Integration Tests │  (15%)
      │                   │  WebApplicationFactory, Testcontainers
      └───────────────────┘
    ┌───────────────────────────┐
    │      Unit Tests           │  (80%)
    │  xUnit, Moq, FluentAssert │
    └───────────────────────────┘
```

## Project Structure

```
tests/
├── Hive.Api.Tests/           # Unit + Integration tests
│   ├── Services/
│   │   ├── CosmosDbServiceTests.cs
│   │   ├── BlobStorageServiceTests.cs
│   │   ├── DocumentServiceTests.cs
│   │   └── ShareServiceTests.cs
│   ├── Endpoints/
│   │   ├── DocumentsEndpointsTests.cs
│   │   ├── UploadEndpointsTests.cs
│   │   └── ShareEndpointsTests.cs
│   └── Integration/
│       ├── DocumentWorkflowTests.cs
│       └── ChunkedUploadTests.cs
│
├── Hive.Functions.Tests/     # Function tests
│   ├── DocumentProcessorFunctionTests.cs
│   ├── OcrServiceTests.cs
│   ├── TaggingServiceTests.cs
│   └── ThumbnailServiceTests.cs
│
└── Hive.E2E.Tests/          # End-to-end tests
    ├── DocumentUploadTests.cs
    ├── DocumentSearchTests.cs
    └── DocumentSharingTests.cs
```

## Unit Tests

Unit tests verify individual components in isolation using mocks.

### Example: CosmosDbService Tests

```csharp
using Xunit;
using Moq;
using FluentAssertions;
using Microsoft.Azure.Cosmos;
using Hive.Api.Services;
using Hive.Api.Models;

namespace Hive.Api.Tests.Services;

public class CosmosDbServiceTests
{
    private readonly Mock<Container> _mockContainer;
    private readonly CosmosDbService _service;

    public CosmosDbServiceTests()
    {
        _mockContainer = new Mock<Container>();
        var mockDatabase = new Mock<Database>();
        var mockClient = new Mock<CosmosClient>();

        mockClient
            .Setup(x => x.GetDatabase(It.IsAny<string>()))
            .Returns(mockDatabase.Object);

        mockDatabase
            .Setup(x => x.GetContainer(It.IsAny<string>()))
            .Returns(_mockContainer.Object);

        _service = new CosmosDbService(mockClient.Object, "testDb");
    }

    [Fact]
    public async Task CreateDocumentAsync_ShouldCreateDocument_WhenValidInput()
    {
        // Arrange
        var document = new Document
        {
            Id = "doc-123",
            UserId = "user-001",
            FileName = "test.pdf",
            FileSize = 1024,
            Status = "uploaded"
        };

        _mockContainer
            .Setup(x => x.CreateItemAsync(
                It.IsAny<Document>(),
                It.IsAny<PartitionKey>(),
                It.IsAny<ItemRequestOptions>(),
                default))
            .ReturnsAsync((Document doc, PartitionKey pk, ItemRequestOptions opts, CancellationToken ct) =>
            {
                var mockResponse = new Mock<ItemResponse<Document>>();
                mockResponse.Setup(x => x.Resource).Returns(doc);
                return mockResponse.Object;
            });

        // Act
        var result = await _service.CreateDocumentAsync(document);

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("doc-123");
        result.FileName.Should().Be("test.pdf");
    }

    [Fact]
    public async Task GetDocumentAsync_ShouldReturnDocument_WhenExists()
    {
        // Arrange
        var expectedDocument = new Document
        {
            Id = "doc-123",
            UserId = "user-001",
            FileName = "test.pdf"
        };

        _mockContainer
            .Setup(x => x.ReadItemAsync<Document>(
                "doc-123",
                new PartitionKey("user-001"),
                null,
                default))
            .ReturnsAsync((string id, PartitionKey pk, ItemRequestOptions opts, CancellationToken ct) =>
            {
                var mockResponse = new Mock<ItemResponse<Document>>();
                mockResponse.Setup(x => x.Resource).Returns(expectedDocument);
                return mockResponse.Object;
            });

        // Act
        var result = await _service.GetDocumentAsync("doc-123", "user-001");

        // Assert
        result.Should().NotBeNull();
        result.Id.Should().Be("doc-123");
    }

    [Fact]
    public async Task GetDocumentAsync_ShouldReturnNull_WhenNotFound()
    {
        // Arrange
        _mockContainer
            .Setup(x => x.ReadItemAsync<Document>(
                "nonexistent",
                new PartitionKey("user-001"),
                null,
                default))
            .ThrowsAsync(new CosmosException(
                "Not found",
                System.Net.HttpStatusCode.NotFound,
                0,
                "",
                0));

        // Act
        var result = await _service.GetDocumentAsync("nonexistent", "user-001");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public async Task CreateDocumentAsync_ShouldThrow_WhenUserIdInvalid(string userId)
    {
        // Arrange
        var document = new Document
        {
            Id = "doc-123",
            UserId = userId,
            FileName = "test.pdf"
        };

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(
            () => _service.CreateDocumentAsync(document));
    }

    [Fact]
    public async Task QueryDocumentsAsync_ShouldReturnFilteredDocuments()
    {
        // Arrange
        var documents = new List<Document>
        {
            new() { Id = "doc-1", UserId = "user-001", Metadata = new() { Category = "Finanse" } },
            new() { Id = "doc-2", UserId = "user-001", Metadata = new() { Category = "IT" } }
        };

        var mockIterator = new Mock<FeedIterator<Document>>();
        mockIterator.SetupSequence(x => x.HasMoreResults)
            .Returns(true)
            .Returns(false);

        var mockFeedResponse = new Mock<FeedResponse<Document>>();
        mockFeedResponse.Setup(x => x.GetEnumerator()).Returns(documents.GetEnumerator());

        mockIterator
            .Setup(x => x.ReadNextAsync(default))
            .ReturnsAsync(mockFeedResponse.Object);

        _mockContainer
            .Setup(x => x.GetItemQueryIterator<Document>(
                It.IsAny<QueryDefinition>(),
                It.IsAny<string>(),
                It.IsAny<QueryRequestOptions>()))
            .Returns(mockIterator.Object);

        // Act
        var result = await _service.QueryDocumentsAsync("user-001", category: "Finanse");

        // Assert
        result.Should().HaveCount(2); // Mock returns all, real query would filter
    }
}
```

### Example: BlobStorageService Tests

```csharp
public class BlobStorageServiceTests
{
    private readonly Mock<BlobContainerClient> _mockContainer;
    private readonly BlobStorageService _service;

    public BlobStorageServiceTests()
    {
        _mockContainer = new Mock<BlobContainerClient>();
        var mockServiceClient = new Mock<BlobServiceClient>();

        mockServiceClient
            .Setup(x => x.GetBlobContainerClient(It.IsAny<string>()))
            .Returns(_mockContainer.Object);

        _service = new BlobStorageService(mockServiceClient.Object);
    }

    [Fact]
    public async Task UploadFileAsync_ShouldUploadBlob_WhenValidInput()
    {
        // Arrange
        var blobPath = "documents/test.pdf";
        var content = new byte[] { 1, 2, 3, 4 };
        var contentType = "application/pdf";

        var mockBlobClient = new Mock<BlobClient>();
        _mockContainer
            .Setup(x => x.GetBlobClient(blobPath))
            .Returns(mockBlobClient.Object);

        mockBlobClient
            .Setup(x => x.UploadAsync(
                It.IsAny<Stream>(),
                It.IsAny<BlobHttpHeaders>(),
                It.IsAny<IDictionary<string, string>>(),
                It.IsAny<BlobRequestConditions>(),
                It.IsAny<IProgress<long>>(),
                It.IsAny<AccessTier?>(),
                It.IsAny<StorageTransferOptions>(),
                default))
            .ReturnsAsync(Mock.Of<Response<BlobContentInfo>>());

        // Act
        await _service.UploadFileAsync(blobPath, content, contentType);

        // Assert
        mockBlobClient.Verify(x => x.UploadAsync(
            It.IsAny<Stream>(),
            It.IsAny<BlobHttpHeaders>(),
            null,
            null,
            null,
            null,
            default(StorageTransferOptions),
            default), Times.Once);
    }

    [Fact]
    public async Task GenerateSasTokenAsync_ShouldReturnValidUrl()
    {
        // Arrange
        var blobPath = "documents/test.pdf";
        var mockBlobClient = new Mock<BlobClient>();

        _mockContainer
            .Setup(x => x.GetBlobClient(blobPath))
            .Returns(mockBlobClient.Object);

        mockBlobClient
            .Setup(x => x.GenerateSasUri(It.IsAny<BlobSasBuilder>()))
            .Returns(new Uri("https://storage.blob.core.windows.net/documents/test.pdf?sv=..."));

        // Act
        var result = await _service.GenerateSasTokenAsync(blobPath, TimeSpan.FromHours(1));

        // Assert
        result.Should().NotBeNullOrEmpty();
        result.Should().StartWith("https://");
        result.Should().Contain("sv=");
    }

    [Fact]
    public async Task DeleteFileAsync_ShouldDeleteBlob()
    {
        // Arrange
        var blobPath = "documents/test.pdf";
        var mockBlobClient = new Mock<BlobClient>();

        _mockContainer
            .Setup(x => x.GetBlobClient(blobPath))
            .Returns(mockBlobClient.Object);

        mockBlobClient
            .Setup(x => x.DeleteIfExistsAsync(
                DeleteSnapshotsOption.IncludeSnapshots,
                null,
                default))
            .ReturnsAsync(Mock.Of<Response<bool>>());

        // Act
        await _service.DeleteFileAsync(blobPath);

        // Assert
        mockBlobClient.Verify(x => x.DeleteIfExistsAsync(
            DeleteSnapshotsOption.IncludeSnapshots,
            null,
            default), Times.Once);
    }
}
```

### Example: DocumentService Tests

```csharp
public class DocumentServiceTests
{
    private readonly Mock<ICosmosDbService> _mockCosmosDb;
    private readonly Mock<IBlobStorageService> _mockBlobStorage;
    private readonly Mock<IQueueService> _mockQueue;
    private readonly DocumentService _service;

    public DocumentServiceTests()
    {
        _mockCosmosDb = new Mock<ICosmosDbService>();
        _mockBlobStorage = new Mock<IBlobStorageService>();
        _mockQueue = new Mock<IQueueService>();

        _service = new DocumentService(
            _mockCosmosDb.Object,
            _mockBlobStorage.Object,
            _mockQueue.Object);
    }

    [Fact]
    public async Task UploadDocumentAsync_ShouldOrchestrate_AllSteps()
    {
        // Arrange
        var userId = "user-001";
        var fileName = "test.pdf";
        var content = new byte[] { 1, 2, 3, 4 };
        var contentType = "application/pdf";

        var expectedDocument = new Document
        {
            Id = It.IsAny<string>(),
            UserId = userId,
            FileName = fileName,
            Status = "uploaded"
        };

        _mockBlobStorage
            .Setup(x => x.UploadFileAsync(It.IsAny<string>(), content, contentType))
            .Returns(Task.CompletedTask);

        _mockCosmosDb
            .Setup(x => x.CreateDocumentAsync(It.IsAny<Document>()))
            .ReturnsAsync((Document doc) => doc);

        _mockQueue
            .Setup(x => x.SendMessageAsync(It.IsAny<string>()))
            .Returns(Task.CompletedTask);

        // Act
        var result = await _service.UploadDocumentAsync(
            userId, fileName, content, contentType);

        // Assert
        result.Should().NotBeNull();
        result.UserId.Should().Be(userId);
        result.FileName.Should().Be(fileName);
        result.Status.Should().Be("uploaded");

        _mockBlobStorage.Verify(x => x.UploadFileAsync(
            It.IsAny<string>(), content, contentType), Times.Once);

        _mockCosmosDb.Verify(x => x.CreateDocumentAsync(
            It.IsAny<Document>()), Times.Once);

        _mockQueue.Verify(x => x.SendMessageAsync(
            It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task DeleteDocumentAsync_ShouldDelete_BothBlobAndMetadata()
    {
        // Arrange
        var document = new Document
        {
            Id = "doc-123",
            UserId = "user-001",
            BlobPath = "documents/test.pdf"
        };

        _mockCosmosDb
            .Setup(x => x.GetDocumentAsync("doc-123", "user-001"))
            .ReturnsAsync(document);

        _mockBlobStorage
            .Setup(x => x.DeleteFileAsync(document.BlobPath))
            .Returns(Task.CompletedTask);

        _mockCosmosDb
            .Setup(x => x.DeleteDocumentAsync("doc-123", "user-001"))
            .Returns(Task.CompletedTask);

        // Act
        await _service.DeleteDocumentAsync("doc-123", "user-001");

        // Assert
        _mockBlobStorage.Verify(x => x.DeleteFileAsync(document.BlobPath), Times.Once);
        _mockCosmosDb.Verify(x => x.DeleteDocumentAsync("doc-123", "user-001"), Times.Once);
    }
}
```

## Integration Tests

Integration tests verify multiple components working together.

### Setup with WebApplicationFactory

```csharp
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.CosmosDb;
using Testcontainers.Azurite;

namespace Hive.Api.Tests.Integration;

public class DocumentApiFactory : WebApplicationFactory<Program>
{
    private readonly CosmosDbContainer _cosmosContainer;
    private readonly AzuriteContainer _azuriteContainer;

    public DocumentApiFactory()
    {
        _cosmosContainer = new CosmosDbBuilder()
            .WithImage("mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest")
            .Build();

        _azuriteContainer = new AzuriteBuilder()
            .WithImage("mcr.microsoft.com/azure-storage/azurite:latest")
            .Build();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Replace with test containers
            services.Configure<CosmosDbConfiguration>(config =>
            {
                config.Endpoint = _cosmosContainer.GetConnectionString();
                config.EnableLocalEmulator = true;
            });

            services.Configure<BlobStorageConfiguration>(config =>
            {
                config.ConnectionString = _azuriteContainer.GetConnectionString();
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _cosmosContainer.StartAsync();
        await _azuriteContainer.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _cosmosContainer.StopAsync();
        await _azuriteContainer.StopAsync();
    }
}

public class DocumentWorkflowTests : IClassFixture<DocumentApiFactory>
{
    private readonly HttpClient _client;
    private readonly DocumentApiFactory _factory;

    public DocumentWorkflowTests(DocumentApiFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task CompleteDocumentWorkflow_ShouldSucceed()
    {
        // 1. Upload document
        var content = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(new byte[] { 1, 2, 3, 4 });
        fileContent.Headers.ContentType = new("application/pdf");
        content.Add(fileContent, "file", "test.pdf");

        var uploadResponse = await _client.PostAsync(
            "/api/documents?userId=user-001", content);

        uploadResponse.Should().BeSuccessful();
        var document = await uploadResponse.Content.ReadFromJsonAsync<Document>();
        document.Should().NotBeNull();
        document.Id.Should().NotBeNullOrEmpty();

        // 2. Get document
        var getResponse = await _client.GetAsync(
            $"/api/documents/{document.Id}?userId=user-001");

        getResponse.Should().BeSuccessful();
        var retrievedDoc = await getResponse.Content.ReadFromJsonAsync<Document>();
        retrievedDoc.Should().NotBeNull();
        retrievedDoc.Id.Should().Be(document.Id);

        // 3. Update metadata
        var metadata = new DocumentMetadata
        {
            Title = "Updated Title",
            Category = "IT",
            Tags = new List<string> { "test" }
        };

        var updateResponse = await _client.PutAsJsonAsync(
            $"/api/documents/{document.Id}?userId=user-001", metadata);

        updateResponse.Should().BeSuccessful();

        // 4. Get preview URL
        var previewResponse = await _client.GetAsync(
            $"/api/documents/{document.Id}/preview?userId=user-001");

        previewResponse.Should().BeSuccessful();
        var preview = await previewResponse.Content.ReadFromJsonAsync<PreviewUrlResponse>();
        preview.PreviewUrl.Should().NotBeNullOrEmpty();

        // 5. Delete document
        var deleteResponse = await _client.DeleteAsync(
            $"/api/documents/{document.Id}?userId=user-001");

        deleteResponse.Should().BeSuccessful();

        // 6. Verify deleted
        var verifyResponse = await _client.GetAsync(
            $"/api/documents/{document.Id}?userId=user-001");

        verifyResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }
}
```

## Azure Functions Tests

```csharp
public class DocumentProcessorFunctionTests
{
    private readonly Mock<ICosmosDbService> _mockCosmosDb;
    private readonly Mock<IBlobStorageService> _mockBlobStorage;
    private readonly Mock<IOcrService> _mockOcr;
    private readonly Mock<ITaggingService> _mockTagging;
    private readonly Mock<IThumbnailService> _mockThumbnail;
    private readonly DocumentProcessorFunction _function;

    public DocumentProcessorFunctionTests()
    {
        _mockCosmosDb = new Mock<ICosmosDbService>();
        _mockBlobStorage = new Mock<IBlobStorageService>();
        _mockOcr = new Mock<IOcrService>();
        _mockTagging = new Mock<ITaggingService>();
        _mockThumbnail = new Mock<IThumbnailService>();

        _function = new DocumentProcessorFunction(
            _mockCosmosDb.Object,
            _mockBlobStorage.Object,
            _mockOcr.Object,
            _mockTagging.Object,
            _mockThumbnail.Object);
    }

    [Fact]
    public async Task Run_ShouldProcessDocument_Successfully()
    {
        // Arrange
        var message = new ProcessingMessage
        {
            DocumentId = "doc-123",
            UserId = "user-001",
            BlobPath = "documents/test.pdf",
            ContentType = "application/pdf"
        };

        var document = new Document
        {
            Id = "doc-123",
            UserId = "user-001",
            Status = "uploaded"
        };

        _mockCosmosDb
            .Setup(x => x.GetDocumentAsync("doc-123", "user-001"))
            .ReturnsAsync(document);

        _mockBlobStorage
            .Setup(x => x.DownloadFileAsync("documents/test.pdf"))
            .ReturnsAsync(new byte[] { 1, 2, 3 });

        _mockOcr
            .Setup(x => x.ExtractTextAsync(It.IsAny<byte[]>(), "application/pdf"))
            .ReturnsAsync("Extracted text");

        _mockTagging
            .Setup(x => x.GenerateTagsAsync("test.pdf", "Extracted text"))
            .ReturnsAsync(new List<string> { "tag1", "tag2" });

        _mockThumbnail
            .Setup(x => x.GenerateThumbnailAsync(It.IsAny<byte[]>(), "application/pdf"))
            .ReturnsAsync(new byte[] { 9, 9, 9 });

        _mockBlobStorage
            .Setup(x => x.UploadFileAsync(It.IsAny<string>(), It.IsAny<byte[]>(), "image/png"))
            .Returns(Task.CompletedTask);

        _mockCosmosDb
            .Setup(x => x.UpdateDocumentAsync(It.IsAny<Document>()))
            .Returns(Task.CompletedTask);

        // Act
        await _function.Run(JsonSerializer.Serialize(message), Mock.Of<FunctionContext>());

        // Assert
        _mockOcr.Verify(x => x.ExtractTextAsync(It.IsAny<byte[]>(), "application/pdf"), Times.Once);
        _mockTagging.Verify(x => x.GenerateTagsAsync(It.IsAny<string>(), "Extracted text"), Times.Once);
        _mockThumbnail.Verify(x => x.GenerateThumbnailAsync(It.IsAny<byte[]>(), "application/pdf"), Times.Once);
        _mockCosmosDb.Verify(x => x.UpdateDocumentAsync(It.Is<Document>(d =>
            d.Status == "processed" &&
            d.ProcessingInfo.ExtractedText == "Extracted text")), Times.Once);
    }
}
```

## E2E Tests with Playwright

```csharp
using Microsoft.Playwright;

namespace Hive.E2E.Tests;

public class DocumentUploadTests : IAsyncLifetime
{
    private IPlaywright _playwright;
    private IBrowser _browser;
    private IBrowserContext _context;
    private IPage _page;

    public async Task InitializeAsync()
    {
        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(new()
        {
            Headless = true
        });
        _context = await _browser.NewContextAsync();
        _page = await _context.NewPageAsync();
    }

    [Fact]
    public async Task UploadDocument_ShouldAppearInList()
    {
        // Navigate to upload page
        await _page.GotoAsync("http://localhost:5173/upload");

        // Upload file
        var fileChooser = await _page.RunAndWaitForFileChooserAsync(async () =>
        {
            await _page.ClickAsync("text=Browse files");
        });

        await fileChooser.SetFilesAsync("testfiles/sample.pdf");

        // Wait for upload to complete
        await _page.WaitForSelectorAsync("text=Upload complete", new()
        {
            Timeout = 30000
        });

        // Navigate to document list
        await _page.ClickAsync("text=Documents");

        // Verify document appears
        await _page.WaitForSelectorAsync("text=sample.pdf");
        var documentRow = await _page.QuerySelectorAsync("text=sample.pdf");
        documentRow.Should().NotBeNull();
    }

    [Fact]
    public async Task SearchDocument_ShouldReturnResults()
    {
        // Navigate to search page
        await _page.GotoAsync("http://localhost:5173/search");

        // Enter search term
        await _page.FillAsync("input[placeholder='Search documents...']", "raport");

        // Submit search
        await _page.ClickAsync("button:has-text('Search')");

        // Wait for results
        await _page.WaitForSelectorAsync(".search-results");

        // Verify results
        var results = await _page.QuerySelectorAllAsync(".search-result-item");
        results.Should().NotBeEmpty();
    }

    public async Task DisposeAsync()
    {
        await _page.CloseAsync();
        await _context.CloseAsync();
        await _browser.CloseAsync();
        _playwright.Dispose();
    }
}
```

## Running Tests

### Unit Tests

```bash
# Run all tests
dotnet test

# Run with coverage
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover

# Run specific test class
dotnet test --filter "FullyQualifiedName~CosmosDbServiceTests"

# Run with verbose output
dotnet test --logger "console;verbosity=detailed"
```

### Integration Tests

```bash
# Start test containers
docker-compose -f docker-compose.test.yml up -d

# Run integration tests
dotnet test --filter "Category=Integration"

# Stop test containers
docker-compose -f docker-compose.test.yml down
```

### E2E Tests

```bash
# Start all services (API, Frontend, Functions)
docker-compose up -d
cd frontend && npm run dev &

# Run E2E tests
dotnet test tests/Hive.E2E.Tests

# Or with Playwright CLI
npx playwright test
```

## Test Coverage Goals

- **Overall**: > 80%
- **Services**: > 90%
- **Endpoints**: > 80%
- **Functions**: > 85%

## Continuous Integration

### GitHub Actions

```yaml
name: Tests

on: [push, pull_request]

jobs:
  unit-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Setup .NET
        uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '8.0.x'

      - name: Run Unit Tests
        run: |
          dotnet test tests/Hive.Api.Tests \
            /p:CollectCoverage=true \
            /p:CoverletOutputFormat=cobertura

      - name: Upload Coverage
        uses: codecov/codecov-action@v3
        with:
          files: ./coverage.cobertura.xml

  integration-tests:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      - name: Start Test Containers
        run: docker-compose -f docker-compose.test.yml up -d

      - name: Run Integration Tests
        run: dotnet test --filter "Category=Integration"

      - name: Stop Containers
        run: docker-compose -f docker-compose.test.yml down
```

## Best Practices

1. **AAA Pattern**: Arrange, Act, Assert
2. **One Assert Per Test**: Focus on single behavior
3. **Descriptive Names**: `MethodName_Scenario_ExpectedBehavior`
4. **Test Data Builders**: Use builders for complex objects
5. **Avoid Test Interdependence**: Each test should be independent
6. **Mock External Dependencies**: Database, APIs, file system
7. **Use Theory for Parameterized Tests**: Test multiple inputs
8. **Clean Up Resources**: Implement IDisposable/IAsyncDisposable
9. **Fast Tests**: Unit tests < 100ms, Integration tests < 5s
10. **Deterministic Tests**: Same input = same output

## Tools

- **xUnit**: Test framework
- **Moq**: Mocking library
- **FluentAssertions**: Assertion library
- **Testcontainers**: Docker containers for integration tests
- **Playwright**: Browser automation for E2E
- **Coverlet**: Code coverage
- **ReportGenerator**: Coverage reports

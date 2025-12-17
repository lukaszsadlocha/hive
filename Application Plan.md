# Application Plan - Hive Document Management System

## 1. Application Overview

### Goal
Document management application with features:
- Document upload (including large files)
- Document metadata storage
- Background document analysis (OCR, auto-tagging)
- CRUD operations on documents
- Document versioning
- Document sharing
- Full-text search
- Document preview in browser
- Dashboards with filtering and sorting

### Technology Stack
- **Backend**: .NET 8 Core with Minimal API
- **Database**: Azure CosmosDB (document metadata)
- **Storage**: Azure Blob Storage (files)
- **Background Processing**: Azure Functions
- **Frontend**: React + TypeScript + Redux Toolkit
- **Local Environment**: Azure Emulators (CosmosDB, Azurite)

---

## 2. System Architecture

### Data Flow Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│                    React Frontend                                │
│              (Redux Toolkit + RTK Query)                         │
└────────────┬───────────────────────────┬────────────────────────┘
             │ REST API                  │
             │                           │
┌────────────▼───────────────────────────▼────────────────────────┐
│              .NET 8 Minimal API Backend                          │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Endpoints (Minimal API):                                │   │
│  │  - DocumentsEndpoints.cs                                 │   │
│  │  - UploadEndpoints.cs (upload w kawałkach)              │   │
│  │  - SearchEndpoints.cs                                    │   │
│  │  - ShareEndpoints.cs                                     │   │
│  │  - VersionEndpoints.cs                                   │   │
│  └──────────────────────────────────────────────────────────┘   │
│  ┌──────────────────────────────────────────────────────────┐   │
│  │  Services (Logika biznesowa):                            │   │
│  │  - DocumentService                                       │   │
│  │  - CosmosDbService                                       │   │
│  │  - BlobStorageService                                    │   │
│  │  - ChunkedUploadService                                  │   │
│  │  - SearchService                                         │   │
│  │  - ShareService                                          │   │
│  └──────────────────────────────────────────────────────────┘   │
└────────┬──────────────────────┬──────────────────┬─────────────┘
         │                      │                  │
         │                      │                  │
    ┌────▼─────┐         ┌─────▼──────┐    ┌─────▼──────────┐
    │ CosmosDB │         │   Blob     │    │  Azure Queue   │
    │(Metadata)│         │  Storage   │    │  Storage       │
    └──────────┘         └────────────┘    └────────┬───────┘
                                                     │ Trigger
                                          ┌──────────▼──────────┐
                                          │  Azure Functions    │
                                          │  - OCR Processing   │
                                          │  - Auto-tagging     │
                                          │  - Thumbnails       │
                                          └──────────┬──────────┘
                                                     │ Update
                                          ┌──────────▼──────────┐
                                          │     CosmosDB        │
                                          └─────────────────────┘
```

### Document Upload Flow (Large File)

1. **Frontend**: User selects file → Redux action splits file into chunks
2. **Frontend**: File divided into chunks (5MB each) → Sequential upload with progress bar
3. **Backend API** (`POST /api/documents/upload/init`): Creates upload session → Returns sessionId
4. **Backend API** (`POST /api/documents/upload/chunk`): Receives chunk → Saves to Blob (temp)
5. **Backend API** (`POST /api/documents/upload/complete`): Finalizes → Merges chunks → Saves metadata to CosmosDB → Sends message to queue
6. **Azure Queue**: Message triggers Azure Function
7. **Azure Function**: Processes document (OCR, tagging) → Updates CosmosDB
8. **Frontend**: Refreshes document status → Shows "Processed"

---

## 3. Project Structure

```
CosmosDbWithFunctions/
├── src/
│   ├── Hive.Api/              # Main API project
│   │   ├── Program.cs                        # Minimal API setup
│   │   ├── appsettings.json
│   │   ├── appsettings.Development.json      # Config for emulators
│   │   │
│   │   ├── Endpoints/                        # Minimal API endpoints
│   │   │   ├── DocumentsEndpoints.cs         # Document CRUD
│   │   │   ├── UploadEndpoints.cs            # Chunked upload
│   │   │   ├── SearchEndpoints.cs            # Search
│   │   │   ├── ShareEndpoints.cs             # Sharing
│   │   │   └── VersionEndpoints.cs           # Versioning
│   │   │
│   │   ├── Services/                         # Business logic
│   │   │   ├── IDocumentService.cs
│   │   │   ├── DocumentService.cs            # Orchestration
│   │   │   ├── ICosmosDbService.cs
│   │   │   ├── CosmosDbService.cs            # ⭐ CosmosDB operations
│   │   │   ├── IBlobStorageService.cs
│   │   │   ├── BlobStorageService.cs         # Blob operations
│   │   │   ├── IChunkedUploadService.cs
│   │   │   ├── ChunkedUploadService.cs       # ⭐ Chunked upload
│   │   │   ├── ISearchService.cs
│   │   │   ├── SearchService.cs              # Full-text search
│   │   │   ├── IShareService.cs
│   │   │   └── ShareService.cs
│   │   │
│   │   ├── Models/                           # DTOs and models
│   │   │   ├── Document.cs                   # Document entity
│   │   │   ├── DocumentVersion.cs
│   │   │   ├── UploadSession.cs
│   │   │   ├── ShareLink.cs
│   │   │   └── DTOs/
│   │   │
│   │   ├── Extensions/
│   │   │   └── ServiceCollectionExtensions.cs # DI registration
│   │   │
│   │   ├── Middleware/
│   │   │   └── ExceptionHandlingMiddleware.cs
│   │   │
│   │   └── Configuration/
│   │       ├── CosmosDbOptions.cs
│   │       ├── BlobStorageOptions.cs
│   │       └── AzureQueueOptions.cs
│   │
│   ├── Hive.Functions/         # Azure Functions
│   │   ├── host.json
│   │   ├── local.settings.json
│   │   │
│   │   ├── Functions/
│   │   │   ├── DocumentProcessorFunction.cs  # ⭐ Main processor
│   │   │   ├── OcrFunction.cs                # OCR
│   │   │   ├── AutoTaggingFunction.cs        # Auto-tagging
│   │   │   └── ThumbnailGeneratorFunction.cs
│   │   │
│   │   ├── Services/
│   │   │   ├── IOcrService.cs
│   │   │   ├── OcrService.cs                 # Azure Computer Vision
│   │   │   ├── ITaggingService.cs
│   │   │   └── TaggingService.cs
│   │   │
│   │   └── Models/
│   │       └── ProcessingMessage.cs          # Queue message format
│   │
│   └── Hive.Shared/            # Shared library
│       └── Models/
│
├── frontend/                                 # React Frontend
│   ├── package.json
│   ├── vite.config.ts
│   │
│   ├── src/
│   │   ├── main.tsx
│   │   ├── App.tsx
│   │   │
│   │   ├── features/                         # Feature-based structure
│   │   │   ├── documents/
│   │   │   │   ├── documentsSlice.ts         # Redux slice
│   │   │   │   ├── documentsApi.ts           # RTK Query API
│   │   │   │   ├── DocumentList.tsx          # List with filtering
│   │   │   │   ├── DocumentUpload.tsx
│   │   │   │   ├── DocumentPreview.tsx
│   │   │   │   └── DocumentDetails.tsx
│   │   │   │
│   │   │   ├── upload/
│   │   │   │   ├── uploadSlice.ts            # ⭐ Chunked upload
│   │   │   │   ├── ChunkedUploader.ts
│   │   │   │   └── UploadProgress.tsx
│   │   │   │
│   │   │   ├── search/
│   │   │   │   ├── searchSlice.ts
│   │   │   │   └── SearchBar.tsx
│   │   │   │
│   │   │   └── share/
│   │   │       ├── shareSlice.ts
│   │   │       └── ShareDialog.tsx
│   │   │
│   │   ├── store/
│   │   │   ├── store.ts                      # Redux store config
│   │   │   └── hooks.ts
│   │   │
│   │   └── services/
│   │       └── api.ts                        # Base API config
│   │
├── tests/
│   ├── Hive.Api.Tests/
│   └── Hive.Functions.Tests/
│
├── docs/                                     # ⭐ Detailed documentation
│   ├── COSMOSDB_GUIDE.md                     # How CosmosDB works
│   ├── AZURE_FUNCTIONS_GUIDE.md              # Functions integration
│   ├── FRONTEND_BACKEND_COMMUNICATION.md     # FE-BE communication
│   ├── LOCAL_DEVELOPMENT.md                  # Emulators setup
│   └── ARCHITECTURE.md
│
├── docker-compose.yml                        # Local emulators
└── Hive.sln
```

---

## 4. Azure CosmosDB - Detailed Description

### 4.1 What is CosmosDB?

Azure CosmosDB is a globally distributed, multi-model NoSQL database from Microsoft. In our application, we use the SQL API (Core API).

**Key Features**:
- **NoSQL**: JSON documents instead of relational tables
- **Partition Key**: Partitioning key for data distribution
- **Indexing**: Automatic indexing of all fields
- **TTL**: Time-to-Live for automatic document deletion
- **Change Feed**: Real-time streaming of changes

### 4.2 Containers in Our Application

#### Container 1: `documents`
**Purpose**: Main document metadata
**Partition Key**: `/userId` (or `/categoryId` for single-user)

**Sample document**:
```json
{
  "id": "doc-123e4567-e89b-12d3-a456-426614174000",
  "type": "document",
  "userId": "user-001",
  "fileName": "Financial_Report_2024.pdf",
  "contentType": "application/pdf",
  "fileSize": 5242880,
  "blobPath": "documents/2024/12/doc-123e4567.pdf",
  "uploadedAt": "2024-12-17T10:30:00Z",
  "currentVersionId": "v3",
  "status": "processed",

  "metadata": {
    "title": "Financial Report 2024",
    "description": "Q4 financial analysis",
    "category": "Finance",
    "tags": ["finance", "2024", "quarterly"],
    "autoTags": ["budget", "revenue"]
  },

  "processing": {
    "ocrCompleted": true,
    "ocrText": "Extracted text...",
    "thumbnailGenerated": true,
    "thumbnailPath": "thumbnails/doc-123e4567.jpg",
    "processedAt": "2024-12-17T10:35:00Z"
  },

  "versions": [
    {
      "versionId": "v1",
      "blobPath": "documents/2024/12/doc-123e4567-v1.pdf",
      "uploadedAt": "2024-12-01T10:00:00Z",
      "comment": "Initial version"
    },
    {
      "versionId": "v2",
      "blobPath": "documents/2024/12/doc-123e4567-v2.pdf",
      "uploadedAt": "2024-12-10T14:20:00Z",
      "comment": "Updated numbers"
    }
  ],

  "search": {
    "fullText": "financial report 2024 q4 revenue budget..."
  }
}
```

**Indexing policy**:
```json
{
  "indexingMode": "consistent",
  "automatic": true,
  "includedPaths": [
    { "path": "/*" }
  ],
  "excludedPaths": [
    { "path": "/processing/ocrText/*" },
    { "path": "/search/fullText/*" }
  ],
  "compositeIndexes": [
    [
      { "path": "/userId", "order": "ascending" },
      { "path": "/uploadedAt", "order": "descending" }
    ],
    [
      { "path": "/metadata/category", "order": "ascending" },
      { "path": "/uploadedAt", "order": "descending" }
    ]
  ]
}
```

#### Container 2: `upload-sessions`
**Purpose**: Tracking chunked upload progress
**Partition Key**: `/sessionId`
**TTL**: 24 hours (auto-cleanup)

```json
{
  "id": "session-abc123",
  "sessionId": "session-abc123",
  "userId": "user-001",
  "fileName": "Large_Video.mp4",
  "totalSize": 524288000,
  "totalChunks": 100,
  "chunkSize": 5242880,
  "uploadedChunks": [1, 2, 3, 4, 5],
  "status": "in-progress",
  "tempBlobContainer": "upload-temp",
  "createdAt": "2024-12-17T10:00:00Z",
  "ttl": 86400
}
```

#### Container 3: `share-links`
**Purpose**: Document sharing links
**Partition Key**: `/linkId`
**TTL**: Configurable

```json
{
  "id": "link-xyz789",
  "linkId": "link-xyz789",
  "documentId": "doc-123e4567",
  "userId": "user-001",
  "token": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "expiresAt": "2024-12-24T10:00:00Z",
  "accessCount": 5,
  "maxAccessCount": 10,
  "permissions": ["view", "download"],
  "ttl": 604800
}
```

### 4.3 Partition Key Strategies

**Why `/userId`?**
- Most queries will be filtered by user
- Efficient queries within a single partition
- Good data distribution in multi-user system

**Alternative for single-user**:
Use a synthetic partition key based on date or category:
```json
{
  "partitionKey": "2024-12",
  "id": "doc-123"
}
```

### 4.4 How to Connect to CosmosDB in .NET

**CosmosDbService.cs - Implementation**:

```csharp
public class CosmosDbService : ICosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly Container _documentsContainer;
    private readonly Container _uploadSessionsContainer;
    private readonly Container _shareLinksContainer;
    private readonly ILogger<CosmosDbService> _logger;

    public CosmosDbService(
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosDbService> logger)
    {
        var config = options.Value;

        // Create CosmosDB client
        var clientOptions = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            ConnectionMode = ConnectionMode.Direct, // Faster connection
            MaxRetryAttemptsOnRateLimitedRequests = 9,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
        };

        // For local emulator - disable certificate validation
        if (config.EnableLocalEmulator)
        {
            clientOptions.HttpClientFactory = () =>
            {
                HttpMessageHandler httpMessageHandler = new HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                };
                return new HttpClient(httpMessageHandler);
            };
        }

        _cosmosClient = new CosmosClient(config.Endpoint, config.Key, clientOptions);
        _database = _cosmosClient.GetDatabase(config.DatabaseName);

        // Container references
        _documentsContainer = _database.GetContainer("documents");
        _uploadSessionsContainer = _database.GetContainer("upload-sessions");
        _shareLinksContainer = _database.GetContainer("share-links");

        _logger = logger;
    }

    // Create document
    public async Task<Document> CreateDocumentAsync(Document document)
    {
        try
        {
            var response = await _documentsContainer.CreateItemAsync(
                document,
                new PartitionKey(document.UserId)
            );

            _logger.LogInformation(
                $"Created document {document.Id}, RU consumed: {response.RequestCharge}"
            );

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.Conflict)
        {
            _logger.LogError($"Document {document.Id} already exists");
            throw;
        }
    }

    // Get document
    public async Task<Document> GetDocumentAsync(string documentId, string userId)
    {
        try
        {
            var response = await _documentsContainer.ReadItemAsync<Document>(
                documentId,
                new PartitionKey(userId)
            );

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning($"Document {documentId} not found");
            return null;
        }
    }

    // Query with filtering and sorting
    public async Task<(List<Document> documents, string continuationToken)>
        QueryDocumentsAsync(
            string userId,
            string category = null,
            string sortBy = "uploadedAt",
            string sortOrder = "DESC",
            int pageSize = 20,
            string continuationToken = null)
    {
        // Build SQL query
        var queryText = "SELECT * FROM c WHERE c.userId = @userId AND c.type = 'document'";

        if (!string.IsNullOrEmpty(category))
        {
            queryText += " AND c.metadata.category = @category";
        }

        queryText += $" ORDER BY c.{sortBy} {sortOrder}";

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@userId", userId);

        if (!string.IsNullOrEmpty(category))
        {
            queryDefinition = queryDefinition.WithParameter("@category", category);
        }

        var queryRequestOptions = new QueryRequestOptions
        {
            PartitionKey = new PartitionKey(userId), // Query within partition
            MaxItemCount = pageSize
        };

        var documents = new List<Document>();
        var iterator = _documentsContainer.GetItemQueryIterator<Document>(
            queryDefinition,
            continuationToken,
            queryRequestOptions
        );

        var response = await iterator.ReadNextAsync();
        documents.AddRange(response);

        _logger.LogInformation($"Query returned {documents.Count} documents, RU: {response.RequestCharge}");

        return (documents, response.ContinuationToken);
    }

    // Full-text search
    public async Task<List<Document>> SearchDocumentsAsync(string searchText, string userId)
    {
        var queryText = @"
            SELECT * FROM c
            WHERE c.userId = @userId
              AND c.type = 'document'
              AND (
                CONTAINS(c.search.fullText, @searchText, true)
                OR CONTAINS(c.fileName, @searchText, true)
                OR CONTAINS(c.metadata.title, @searchText, true)
              )
            ORDER BY c.uploadedAt DESC";

        var queryDefinition = new QueryDefinition(queryText)
            .WithParameter("@userId", userId)
            .WithParameter("@searchText", searchText);

        var results = new List<Document>();
        var iterator = _documentsContainer.GetItemQueryIterator<Document>(
            queryDefinition,
            requestOptions: new QueryRequestOptions
            {
                PartitionKey = new PartitionKey(userId)
            }
        );

        while (iterator.HasMoreResults)
        {
            var response = await iterator.ReadNextAsync();
            results.AddRange(response);
        }

        return results;
    }

    // Update document
    public async Task<Document> UpdateDocumentAsync(Document document)
    {
        var response = await _documentsContainer.ReplaceItemAsync(
            document,
            document.Id,
            new PartitionKey(document.UserId)
        );

        return response.Resource;
    }

    // Delete document
    public async Task DeleteDocumentAsync(string documentId, string userId)
    {
        await _documentsContainer.DeleteItemAsync<Document>(
            documentId,
            new PartitionKey(userId)
        );

        _logger.LogInformation($"Deleted document {documentId}");
    }

    // Initialize database (development only)
    public async Task InitializeDatabaseAsync()
    {
        // Create database
        var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
            "HiveDb",
            throughput: 400 // Shared throughput
        );

        var database = databaseResponse.Database;

        // Container: documents
        var documentsContainerProperties = new ContainerProperties
        {
            Id = "documents",
            PartitionKeyPath = "/userId",
            IndexingPolicy = new IndexingPolicy
            {
                IndexingMode = IndexingMode.Consistent,
                Automatic = true,
                IncludedPaths = { new IncludedPath { Path = "/*" } },
                ExcludedPaths =
                {
                    new ExcludedPath { Path = "/processing/ocrText/*" },
                    new ExcludedPath { Path = "/search/fullText/*" }
                },
                CompositeIndexes =
                {
                    new Collection<CompositePath>
                    {
                        new CompositePath { Path = "/userId", Order = CompositePathSortOrder.Ascending },
                        new CompositePath { Path = "/uploadedAt", Order = CompositePathSortOrder.Descending }
                    }
                }
            }
        };

        await database.CreateContainerIfNotExistsAsync(documentsContainerProperties);

        // Container: upload-sessions (with TTL)
        var uploadSessionsProperties = new ContainerProperties
        {
            Id = "upload-sessions",
            PartitionKeyPath = "/sessionId",
            DefaultTimeToLive = 86400 // 24 hours
        };

        await database.CreateContainerIfNotExistsAsync(uploadSessionsProperties);

        // Container: share-links (with TTL per item)
        var shareLinksProperties = new ContainerProperties
        {
            Id = "share-links",
            PartitionKeyPath = "/linkId",
            DefaultTimeToLive = -1 // TTL per item
        };

        await database.CreateContainerIfNotExistsAsync(shareLinksProperties);

        _logger.LogInformation("CosmosDB initialized successfully");
    }
}
```

**Configuration in appsettings.Development.json**:
```json
{
  "CosmosDb": {
    "Endpoint": "https://localhost:8081",
    "Key": "C2y6yDjf5/R+ob0N8A7Cgv3/l+Hb+0tTNUKF2d5EWlG3ScYfzHtOVIxd39LhNVS6/zDzXy3z5wXvjK7tZOVqXQ==",
    "DatabaseName": "HiveDb",
    "EnableLocalEmulator": true
  }
}
```

**Registration in Program.cs**:
```csharp
builder.Services.Configure<CosmosDbOptions>(
    builder.Configuration.GetSection("CosmosDb")
);
builder.Services.AddSingleton<ICosmosDbService, CosmosDbService>();
```

### 4.5 Key CosmosDB Concepts

#### Request Units (RU)
- CosmosDB charges in RU (Request Units)
- Read 1KB document = ~1 RU
- Write 1KB document = ~5 RU
- Cross-partition queries = more RU
- **Optimization**: Always use partition key in queries

#### Partition Key Strategy
```
Good practices:
✅ Choose a key with high cardinality (many unique values)
✅ Even distribution of queries
✅ Most queries within a single partition

Bad practices:
❌ Partition key with few values (e.g., status: "active"/"inactive")
❌ Hot partitions (one partition receives most requests)
```

#### Change Feed
CosmosDB Change Feed allows real-time streaming of changes:

```csharp
// In Azure Function
[FunctionName("DocumentChangeFeedHandler")]
public static void Run(
    [CosmosDBTrigger(
        databaseName: "HiveDb",
        containerName: "documents",
        Connection = "CosmosDBConnection",
        LeaseContainerName = "leases",
        CreateLeaseContainerIfNotExists = true)]
    IReadOnlyList<Document> input,
    ILogger log)
{
    foreach (var document in input)
    {
        log.LogInformation($"Document modified: {document.Id}");
        // React to changes (e.g., send notification)
    }
}
```

---

## 5. Azure Functions - Detailed Description

### 5.1 What are Azure Functions?

Azure Functions is a serverless platform for running code in the cloud without managing infrastructure.

**Key Features**:
- **Event-driven**: Functions triggered by events (queue, timer, HTTP, CosmosDB)
- **Serverless**: No server management
- **Auto-scaling**: Automatic scaling
- **Pay-per-execution**: Pay only for executions

### 5.2 Functions in Our Application

#### Function 1: Document Processor (Queue Trigger)

**DocumentProcessorFunction.cs**:

```csharp
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

public class DocumentProcessorFunction
{
    private readonly ICosmosDbService _cosmosDbService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IOcrService _ocrService;
    private readonly ITaggingService _taggingService;
    private readonly IThumbnailService _thumbnailService;
    private readonly ILogger<DocumentProcessorFunction> _logger;

    public DocumentProcessorFunction(
        ICosmosDbService cosmosDbService,
        IBlobStorageService blobStorageService,
        IOcrService ocrService,
        ITaggingService taggingService,
        IThumbnailService thumbnailService,
        ILogger<DocumentProcessorFunction> logger)
    {
        _cosmosDbService = cosmosDbService;
        _blobStorageService = blobStorageService;
        _ocrService = ocrService;
        _taggingService = taggingService;
        _thumbnailService = thumbnailService;
        _logger = logger;
    }

    [Function("ProcessDocument")]
    public async Task Run(
        [QueueTrigger("document-processing-queue", Connection = "AzureWebJobsStorage")]
        ProcessingMessage message)
    {
        _logger.LogInformation($"[START] Processing document: {message.DocumentId}");

        try
        {
            // STEP 1: Get metadata from CosmosDB
            var document = await _cosmosDbService.GetDocumentAsync(
                message.DocumentId,
                message.UserId
            );

            if (document == null)
            {
                _logger.LogError($"Document {message.DocumentId} not found in CosmosDB");
                throw new Exception("Document not found");
            }

            // STEP 2: Download file from Blob Storage
            _logger.LogInformation($"Downloading blob: {document.BlobPath}");
            using var fileStream = await _blobStorageService.DownloadAsync(document.BlobPath);

            // STEP 3: OCR (if PDF or image)
            string extractedText = null;
            if (IsOcrSupported(document.ContentType))
            {
                _logger.LogInformation("Starting OCR extraction...");
                extractedText = await _ocrService.ExtractTextAsync(fileStream);
                _logger.LogInformation($"OCR completed. Extracted {extractedText?.Length ?? 0} characters");
            }

            // STEP 4: Generate thumbnail
            _logger.LogInformation("Generating thumbnail...");
            fileStream.Position = 0; // Reset stream
            var thumbnailPath = await _thumbnailService.GenerateThumbnailAsync(
                fileStream,
                document.Id
            );
            _logger.LogInformation($"Thumbnail saved: {thumbnailPath}");

            // STEP 5: Auto-tagging
            _logger.LogInformation("Generating auto-tags...");
            var autoTags = await _taggingService.GenerateTagsAsync(
                extractedText ?? document.FileName
            );
            _logger.LogInformation($"Generated {autoTags.Count} tags: {string.Join(", ", autoTags)}");

            // STEP 6: Update document in CosmosDB
            document.Processing = new ProcessingInfo
            {
                OcrCompleted = extractedText != null,
                OcrText = extractedText,
                ThumbnailGenerated = !string.IsNullOrEmpty(thumbnailPath),
                ThumbnailPath = thumbnailPath,
                AutoTaggingCompleted = true,
                ProcessedAt = DateTime.UtcNow
            };

            document.Metadata.AutoTags = autoTags;
            document.Status = "processed";

            // Add to searchable text
            document.Search = new SearchInfo
            {
                FullText = $"{document.FileName} {document.Metadata.Title} {extractedText}".ToLower()
            };

            await _cosmosDbService.UpdateDocumentAsync(document);

            _logger.LogInformation($"[SUCCESS] Document {message.DocumentId} processed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"[FAILED] Error processing document {message.DocumentId}");

            // Update status to "failed"
            try
            {
                var document = await _cosmosDbService.GetDocumentAsync(
                    message.DocumentId,
                    message.UserId
                );

                if (document != null)
                {
                    document.Status = "failed";
                    await _cosmosDbService.UpdateDocumentAsync(document);
                }
            }
            catch (Exception updateEx)
            {
                _logger.LogError(updateEx, "Failed to update document status to failed");
            }

            // Throw exception - Azure Functions will automatically retry
            throw;
        }
    }

    private bool IsOcrSupported(string contentType)
    {
        return contentType switch
        {
            "application/pdf" => true,
            "image/jpeg" => true,
            "image/png" => true,
            "image/tiff" => true,
            _ => false
        };
    }
}
```

**ProcessingMessage.cs**:
```csharp
public class ProcessingMessage
{
    public string DocumentId { get; set; }
    public string UserId { get; set; }
    public string BlobPath { get; set; }
    public string ContentType { get; set; }
    public DateTime EnqueuedAt { get; set; }
}
```

### 5.3 How the API Sends Messages to the Queue

**In DocumentService.cs**:

```csharp
using Azure.Storage.Queues;
using System.Text.Json;

public class DocumentService : IDocumentService
{
    private readonly QueueClient _queueClient;

    public DocumentService(IConfiguration configuration)
    {
        var connectionString = configuration.GetValue<string>("AzureQueue:ConnectionString");
        var queueName = configuration.GetValue<string>("AzureQueue:QueueName");

        _queueClient = new QueueClient(connectionString, queueName);
        _queueClient.CreateIfNotExists();
    }

    public async Task<Document> CompleteUploadAsync(string sessionId)
    {
        // ... logic for merging chunks and saving to CosmosDB ...

        var document = await _cosmosDbService.CreateDocumentAsync(newDocument);

        // Send message to queue
        var message = new ProcessingMessage
        {
            DocumentId = document.Id,
            UserId = document.UserId,
            BlobPath = document.BlobPath,
            ContentType = document.ContentType,
            EnqueuedAt = DateTime.UtcNow
        };

        var messageJson = JsonSerializer.Serialize(message);
        var messageBytes = Encoding.UTF8.GetBytes(messageJson);
        var base64Message = Convert.ToBase64String(messageBytes);

        await _queueClient.SendMessageAsync(base64Message);

        _logger.LogInformation($"Enqueued processing message for document {document.Id}");

        return document;
    }
}
```

### 5.4 Communication Flow

```
┌──────────────┐
│   API        │
│ DocumentService │
└──────┬───────┘
       │ SendMessageAsync()
       ▼
┌──────────────┐
│ Azure Queue  │
│  Storage     │
└──────┬───────┘
       │ Queue Trigger (polling every few seconds)
       ▼
┌──────────────┐
│ Azure        │
│ Function     │
│ DocumentProcessor │
└──────┬───────┘
       │
       ├─► BlobStorageService (download file)
       ├─► OcrService (text extraction)
       ├─► TaggingService (auto-tags)
       ├─► ThumbnailService (thumbnail)
       │
       ▼
┌──────────────┐
│  CosmosDB    │
│ (update metadata) │
└──────────────┘
```

### 5.5 Configuration for Local Development

**local.settings.json** (in Functions project):
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "CosmosDBConnection": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5/R+ob0N8A7Cgv3/l+Hb+0tTNUKF2d5EWlG3ScYfzHtOVIxd39LhNVS6/zDzXy3z5wXvjK7tZOVqXQ==",
    "BlobStorageConnection": "UseDevelopmentStorage=true"
  }
}
```

**Running locally**:
```bash
# In directory src/Hive.Functions
func start

# Output:
# Azure Functions Core Tools
# Core Tools Version: 4.x
# Function Runtime Version: 4.x
#
# Functions:
#   ProcessDocument: queueTrigger
#
# For detailed output, run func with --verbose flag.
```

### 5.6 Retry Policy

Azure Functions automatycznie ponawia wywołania w przypadku błędu:

**host.json**:
```json
{
  "version": "2.0",
  "extensions": {
    "queues": {
      "maxDequeueCount": 5,
      "visibilityTimeout": "00:00:30"
    }
  }
}
```

- **maxDequeueCount**: Maximum number of attempts (5)
- **visibilityTimeout**: Time before retry (30 seconds)
- After 5 failed attempts, message goes to **poison queue**: `document-processing-queue-poison`

---

## 6. Frontend - React + Redux Toolkit

### 6.1 Communication with Backend API

#### RTK Query Configuration

**src/services/api.ts**:
```typescript
import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

export const baseApi = createApi({
  reducerPath: 'api',
  baseQuery: fetchBaseQuery({
    baseUrl: import.meta.env.VITE_API_URL || 'http://localhost:5000/api',
    prepareHeaders: (headers) => {
      // Add headers if needed (e.g., Authorization in the future)
      headers.set('Content-Type', 'application/json');
      return headers;
    },
  }),
  tagTypes: ['Document', 'Documents', 'UploadSession'],
  endpoints: () => ({}),
});
```

**src/features/documents/documentsApi.ts**:
```typescript
import { baseApi } from '../../services/api';
import type {
  Document,
  SearchRequest,
  SearchResponse,
  CreateShareLinkRequest,
  ShareLinkResponse
} from './types';

export const documentsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // GET /api/documents - Document list
    getDocuments: builder.query<SearchResponse, {
      page?: number;
      pageSize?: number;
      category?: string;
      sortBy?: string;
      sortOrder?: 'asc' | 'desc';
    }>({
      query: (params) => ({
        url: '/documents',
        params,
      }),
      providesTags: ['Documents'],
    }),

    // GET /api/documents/{id} - Single document
    getDocument: builder.query<Document, string>({
      query: (id) => `/documents/${id}`,
      providesTags: (result, error, id) => [{ type: 'Document', id }],
    }),

    // DELETE /api/documents/{id}
    deleteDocument: builder.mutation<void, string>({
      query: (id) => ({
        url: `/documents/${id}`,
        method: 'DELETE',
      }),
      invalidatesTags: ['Documents'],
    }),

    // PUT /api/documents/{id}
    updateDocument: builder.mutation<Document, {
      id: string;
      data: Partial<Document>
    }>({
      query: ({ id, data }) => ({
        url: `/documents/${id}`,
        method: 'PUT',
        body: data,
      }),
      invalidatesTags: (result, error, { id }) => [
        { type: 'Document', id },
        'Documents',
      ],
    }),

    // POST /api/search
    searchDocuments: builder.query<SearchResponse, SearchRequest>({
      query: (searchRequest) => ({
        url: '/search',
        method: 'POST',
        body: searchRequest,
      }),
    }),

    // GET /api/documents/{id}/preview
    getDocumentPreview: builder.query<{ url: string }, string>({
      query: (id) => `/documents/${id}/preview`,
    }),

    // POST /api/share
    createShareLink: builder.mutation<ShareLinkResponse, CreateShareLinkRequest>({
      query: (data) => ({
        url: '/share',
        method: 'POST',
        body: data,
      }),
    }),
  }),
});

export const {
  useGetDocumentsQuery,
  useGetDocumentQuery,
  useDeleteDocumentMutation,
  useUpdateDocumentMutation,
  useLazySearchDocumentsQuery,
  useLazyGetDocumentPreviewQuery,
  useCreateShareLinkMutation,
} = documentsApi;
```

### 6.2 Chunked Upload

**src/features/upload/uploadSlice.ts**:

```typescript
import { createSlice, createAsyncThunk, PayloadAction } from '@reduxjs/toolkit';
import axios from 'axios';

const API_URL = import.meta.env.VITE_API_URL || 'http://localhost:5000/api';
const CHUNK_SIZE = 5 * 1024 * 1024; // 5MB

interface UploadProgress {
  sessionId: string;
  fileName: string;
  fileSize: number;
  progress: number;
  status: 'idle' | 'uploading' | 'processing' | 'completed' | 'failed';
  error?: string;
  documentId?: string;
}

interface UploadState {
  uploads: Record<string, UploadProgress>;
}

const initialState: UploadState = {
  uploads: {},
};

// Async thunk for chunked upload
export const uploadFileChunked = createAsyncThunk(
  'upload/chunked',
  async (file: File, { dispatch, rejectWithValue }) => {
    const totalChunks = Math.ceil(file.size / CHUNK_SIZE);

    try {
      // STEP 1: Initialize upload session
      console.log(`[UPLOAD] Initializing upload for ${file.name}`);
      const initResponse = await axios.post(`${API_URL}/documents/upload/init`, {
        fileName: file.name,
        contentType: file.type,
        totalSize: file.size,
        totalChunks,
      });

      const { sessionId } = initResponse.data;
      console.log(`[UPLOAD] Session created: ${sessionId}`);

      // Start tracking in Redux
      dispatch(uploadSlice.actions.uploadStarted({
        sessionId,
        fileName: file.name,
        fileSize: file.size,
      }));

      // STEP 2: Upload chunks sequentially
      for (let chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++) {
        const start = chunkIndex * CHUNK_SIZE;
        const end = Math.min(start + CHUNK_SIZE, file.size);
        const chunk = file.slice(start, end);

        console.log(`[UPLOAD] Uploading chunk ${chunkIndex + 1}/${totalChunks}`);

        const formData = new FormData();
        formData.append('chunk', chunk);
        formData.append('chunkIndex', chunkIndex.toString());
        formData.append('sessionId', sessionId);

        await axios.post(`${API_URL}/documents/upload/chunk`, formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
        });

        // Update progress
        const progress = ((chunkIndex + 1) / totalChunks) * 100;
        dispatch(uploadSlice.actions.uploadProgress({
          sessionId,
          progress,
        }));
      }

      console.log(`[UPLOAD] All chunks uploaded. Completing...`);

      // STEP 3: Finalize upload
      const completeResponse = await axios.post(`${API_URL}/documents/upload/complete`, {
        sessionId,
      });

      const { documentId } = completeResponse.data;
      console.log(`[UPLOAD] Upload completed. Document ID: ${documentId}`);

      return { sessionId, documentId };
    } catch (error: any) {
      console.error('[UPLOAD] Error:', error);
      return rejectWithValue(error.response?.data || error.message);
    }
  }
);

const uploadSlice = createSlice({
  name: 'upload',
  initialState,
  reducers: {
    uploadStarted: (state, action: PayloadAction<{
      sessionId: string;
      fileName: string;
      fileSize: number;
    }>) => {
      state.uploads[action.payload.sessionId] = {
        sessionId: action.payload.sessionId,
        fileName: action.payload.fileName,
        fileSize: action.payload.fileSize,
        progress: 0,
        status: 'uploading',
      };
    },

    uploadProgress: (state, action: PayloadAction<{
      sessionId: string;
      progress: number;
    }>) => {
      const upload = state.uploads[action.payload.sessionId];
      if (upload) {
        upload.progress = action.payload.progress;
      }
    },

    clearUpload: (state, action: PayloadAction<string>) => {
      delete state.uploads[action.payload];
    },
  },

  extraReducers: (builder) => {
    builder
      .addCase(uploadFileChunked.fulfilled, (state, action) => {
        const { sessionId, documentId } = action.payload;
        const upload = state.uploads[sessionId];
        if (upload) {
          upload.status = 'processing';
          upload.documentId = documentId;
          upload.progress = 100;
        }
      })
      .addCase(uploadFileChunked.rejected, (state, action) => {
        // Find upload with "uploading" status
        const sessionId = Object.keys(state.uploads).find(
          (id) => state.uploads[id].status === 'uploading'
        );
        if (sessionId) {
          state.uploads[sessionId].status = 'failed';
          state.uploads[sessionId].error = action.payload as string;
        }
      });
  },
});

export const { clearUpload } = uploadSlice.actions;
export default uploadSlice.reducer;
```

**Upload Component**:

```typescript
// src/features/upload/DocumentUpload.tsx
import React, { useCallback } from 'react';
import { useDropzone } from 'react-dropzone';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { uploadFileChunked } from './uploadSlice';

export const DocumentUpload: React.FC = () => {
  const dispatch = useAppDispatch();
  const uploads = useAppSelector((state) => state.upload.uploads);

  const onDrop = useCallback((acceptedFiles: File[]) => {
    acceptedFiles.forEach((file) => {
      dispatch(uploadFileChunked(file));
    });
  }, [dispatch]);

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    maxSize: 500 * 1024 * 1024, // 500MB
  });

  return (
    <div>
      <div
        {...getRootProps()}
        style={{
          border: '2px dashed #ccc',
          padding: '40px',
          textAlign: 'center',
          cursor: 'pointer',
          backgroundColor: isDragActive ? '#f0f0f0' : 'white',
        }}
      >
        <input {...getInputProps()} />
        <p>
          {isDragActive
            ? 'Drop files here...'
            : 'Drag and drop files or click to select'}
        </p>
      </div>

      {/* Progress bars */}
      <div style={{ marginTop: '20px' }}>
        {Object.values(uploads).map((upload) => (
          <div key={upload.sessionId} style={{ marginBottom: '15px' }}>
            <div style={{ display: 'flex', justifyContent: 'space-between' }}>
              <span>{upload.fileName}</span>
              <span>{upload.status}</span>
            </div>
            <div
              style={{
                width: '100%',
                height: '20px',
                backgroundColor: '#e0e0e0',
                borderRadius: '10px',
                overflow: 'hidden',
              }}
            >
              <div
                style={{
                  width: `${upload.progress}%`,
                  height: '100%',
                  backgroundColor:
                    upload.status === 'failed'
                      ? '#f44336'
                      : upload.status === 'completed'
                      ? '#4caf50'
                      : '#2196f3',
                  transition: 'width 0.3s ease',
                }}
              />
            </div>
            {upload.error && (
              <div style={{ color: 'red', marginTop: '5px' }}>
                Error: {upload.error}
              </div>
            )}
          </div>
        ))}
      </div>
    </div>
  );
};
```

### 6.3 How Frontend-Backend Communication Works

```
┌─────────────────────────────────────────────────────────────┐
│                      FRONTEND (React)                        │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  Komponent: DocumentList.tsx                                │
│  ┌────────────────────────────────────────────┐            │
│  │ useGetDocumentsQuery({ page: 1 })          │            │
│  └──────────────┬─────────────────────────────┘            │
│                 │                                            │
│  RTK Query API  ▼                                           │
│  ┌────────────────────────────────────────────┐            │
│  │ documentsApi.getDocuments                  │            │
│  │ - Caches results                           │            │
│  │ - Handles loading/error states             │            │
│  │ - Automatic refetching                     │            │
│  └──────────────┬─────────────────────────────┘            │
│                 │ HTTP GET                                  │
└─────────────────┼─────────────────────────────────────────┘
                  │
                  │ GET /api/documents?page=1&pageSize=20
                  │
┌─────────────────▼─────────────────────────────────────────┐
│                    BACKEND (.NET API)                      │
├────────────────────────────────────────────────────────────┤
│                                                             │
│  Endpoint: DocumentsEndpoints.cs                           │
│  ┌────────────────────────────────────────────┐           │
│  │ app.MapGet("/api/documents", async (...) =>│           │
│  └──────────────┬─────────────────────────────┘           │
│                 │                                           │
│  Service        ▼                                          │
│  ┌────────────────────────────────────────────┐           │
│  │ documentService.GetDocumentsAsync()        │           │
│  └──────────────┬─────────────────────────────┘           │
│                 │                                           │
│  CosmosDB       ▼                                          │
│  ┌────────────────────────────────────────────┐           │
│  │ cosmosDbService.QueryDocumentsAsync()      │           │
│  │ - Executes SQL query                       │           │
│  │ - Returns document list                    │           │
│  └──────────────┬─────────────────────────────┘           │
│                 │                                           │
│                 │ Returns JSON response                    │
└─────────────────┼─────────────────────────────────────────┘
                  │
                  │ Response: { documents: [...], total: 42 }
                  │
┌─────────────────▼─────────────────────────────────────────┐
│                      FRONTEND                              │
│  RTK Query automatically:                                  │
│  1. Parses JSON response                                   │
│  2. Updates cache                                          │
│  3. Updates component (re-render)                          │
│  4. Sets isLoading = false                                │
└────────────────────────────────────────────────────────────┘
```

### 6.4 Key Advantages of RTK Query

1. **Automatic caching**: You don't need to manually manage cache
2. **Loading states**: Automatic `isLoading`, `isFetching`, `error`
3. **Refetching**: Automatic data refresh
4. **Optimistic updates**: Update UI before receiving response
5. **Tag invalidation**: Automatic refetching after mutations

**Example with automatic refetch**:
```typescript
// After deleting a document...
const [deleteDocument] = useDeleteDocumentMutation();

await deleteDocument(documentId);
// RTK Query automatically refetches the document list
// because deleteDocument invalidates the "Documents" tag
```

---

## 7. Local Development Environment

### 7.1 Required Tools

1. **.NET 8 SDK**: [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
2. **Node.js 18+**: [https://nodejs.org/](https://nodejs.org/)
3. **Azure Functions Core Tools**:
   ```bash
   npm install -g azure-functions-core-tools@4
   ```
4. **Docker Desktop**: [https://www.docker.com/products/docker-desktop](https://www.docker.com/products/docker-desktop)

### 7.2 Docker Compose for Emulators

**docker-compose.yml**:
```yaml
version: '3.8'

services:
  # Azure Cosmos DB Emulator
  cosmosdb:
    image: mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:latest
    container_name: cosmosdb-emulator
    ports:
      - "8081:8081"
      - "10251-10254:10251-10254"
    environment:
      - AZURE_COSMOS_EMULATOR_PARTITION_COUNT=10
      - AZURE_COSMOS_EMULATOR_ENABLE_DATA_PERSISTENCE=true
      - AZURE_COSMOS_EMULATOR_IP_ADDRESS_OVERRIDE=127.0.0.1
    volumes:
      - cosmosdb-data:/data

  # Azurite - Azure Storage Emulator
  azurite:
    image: mcr.microsoft.com/azure-storage/azurite:latest
    container_name: azurite
    ports:
      - "10000:10000"  # Blob service
      - "10001:10001"  # Queue service
      - "10002:10002"  # Table service
    volumes:
      - azurite-data:/data
    command: "azurite --blobHost 0.0.0.0 --queueHost 0.0.0.0 --loose"

volumes:
  cosmosdb-data:
  azurite-data:
```

### 7.3 Running the Application Locally

**Step 1: Start emulators**
```bash
docker-compose up -d
```

**Step 2: Run Backend API**
```bash
cd src/Hive.Api
dotnet restore
dotnet run

# API available at: https://localhost:5001
```

**Step 3: Run Azure Functions**
```bash
cd src/Hive.Functions
func start

# Functions available at: http://localhost:7071
```

**Step 4: Run Frontend**
```bash
cd frontend
npm install
npm run dev

# Frontend available at: http://localhost:5173
```

### 7.4 Emulator Verification

**CosmosDB Emulator**:
- Data Explorer: https://localhost:8081/_explorer/index.html
- Connection string: Check in `appsettings.Development.json`

**Azurite**:
```bash
# Test Blob Storage
curl http://localhost:10000/devstoreaccount1?comp=list

# Test Queue Storage
curl http://localhost:10001/devstoreaccount1?comp=list
```

### 7.5 Connection Strings for Local Development

**appsettings.Development.json**:
```json
{
  "CosmosDb": {
    "Endpoint": "https://localhost:8081",
    "Key": "C2y6yDjf5/R+ob0N8A7Cgv3/l+Hb+0tTNUKF2d5EWlG3ScYfzHtOVIxd39LhNVS6/zDzXy3z5wXvjK7tZOVqXQ==",
    "DatabaseName": "HiveDb",
    "EnableLocalEmulator": true
  },
  "BlobStorage": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "ContainerNames": {
      "Documents": "documents",
      "Thumbnails": "thumbnails",
      "UploadTemp": "upload-temp"
    }
  },
  "AzureQueue": {
    "ConnectionString": "UseDevelopmentStorage=true",
    "QueueName": "document-processing-queue"
  }
}
```

**frontend/.env.development**:
```
VITE_API_URL=http://localhost:5000/api
```

---

## 8. Step-by-Step Implementation Plan

### Phase 1: Foundation (Days 1-3)

#### ✅ Task 1.1: Solution Structure
```bash
# Create solution and projects
dotnet new sln -n Hive
dotnet new webapi -n Hive.Api -o src/Hive.Api
dotnet new func -n Hive.Functions -o src/Hive.Functions
dotnet new classlib -n Hive.Shared -o src/Hive.Shared

dotnet sln add src/Hive.Api
dotnet sln add src/Hive.Functions
dotnet sln add src/Hive.Shared
```

**NuGet packages to install**:
```bash
# API project
cd src/Hive.Api
dotnet add package Microsoft.Azure.Cosmos
dotnet add package Azure.Storage.Blobs
dotnet add package Azure.Storage.Queues

# Functions project
cd ../Hive.Functions
dotnet add package Microsoft.Azure.Functions.Worker
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.CosmosDB
dotnet add package Microsoft.Azure.Functions.Worker.Extensions.Storage.Queues
dotnet add package Azure.Storage.Blobs
```

#### ✅ Task 1.2: Configuration and Models
**Files to create**:
- `Configuration/CosmosDbOptions.cs`
- `Configuration/BlobStorageOptions.cs`
- `Configuration/AzureQueueOptions.cs`
- `Models/Document.cs`
- `Models/DocumentVersion.cs`
- `Models/UploadSession.cs`
- `Models/ShareLink.cs`

#### ✅ Task 1.3: Docker Compose
**Files to create**:
- `docker-compose.yml` (in root directory)

**Running**:
```bash
docker-compose up -d
```

### Phase 2: Backend Services (Days 4-7)

#### ✅ Task 2.1: CosmosDbService
**File**: `src/Hive.Api/Services/CosmosDbService.cs`

**Methods to implement**:
- `InitializeDatabaseAsync()` - create database and containers
- `CreateDocumentAsync()`
- `GetDocumentAsync()`
- `UpdateDocumentAsync()`
- `DeleteDocumentAsync()`
- `QueryDocumentsAsync()` - with filtering, sorting, pagination
- `SearchDocumentsAsync()` - full-text search

#### ✅ Task 2.2: BlobStorageService
**File**: `src/Hive.Api/Services/BlobStorageService.cs`

**Methods to implement**:
- `UploadAsync()` - single file upload
- `DownloadAsync()`
- `DeleteAsync()`
- `GenerateSasTokenAsync()` - for preview URLs
- `UploadChunkAsync()` - upload single chunk
- `MergeChunksAsync()` - merge chunks

#### ✅ Task 2.3: ChunkedUploadService
**File**: `src/Hive.Api/Services/ChunkedUploadService.cs`

**Methods to implement**:
- `InitializeUploadSessionAsync()` - start session
- `UploadChunkAsync()` - save chunk and update session
- `CompleteUploadAsync()` - merge chunks
- `GetUploadProgressAsync()` - progress tracking
- `CleanupFailedUploadAsync()` - cleanup temp files

#### ✅ Task 2.4: DocumentService (Orchestration)
**File**: `src/Hive.Api/Services/DocumentService.cs`

**Methods to implement**:
- `CreateDocumentAsync()` - coordinates blob upload + CosmosDB insert + queue message
- `GetDocumentWithPreviewUrlAsync()` - document + SAS URL
- `UpdateDocumentMetadataAsync()`
- `DeleteDocumentAsync()` - deletes blob + CosmosDB + cleanup
- `CreateNewVersionAsync()` - versioning

### Phase 3: API Endpoints (Days 8-10)

#### ✅ Task 3.1: DocumentsEndpoints (Minimal API)
**File**: `src/Hive.Api/Endpoints/DocumentsEndpoints.cs`

```csharp
public static class DocumentsEndpoints
{
    public static void MapDocumentsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/documents").WithTags("Documents");

        // GET /api/documents
        group.MapGet("/", GetDocuments);

        // GET /api/documents/{id}
        group.MapGet("/{id}", GetDocument);

        // POST /api/documents (simple upload)
        group.MapPost("/", CreateDocument);

        // PUT /api/documents/{id}
        group.MapPut("/{id}", UpdateDocument);

        // DELETE /api/documents/{id}
        group.MapDelete("/{id}", DeleteDocument);
    }
}
```

#### ✅ Task 3.2: UploadEndpoints
**File**: `src/Hive.Api/Endpoints/UploadEndpoints.cs`

**Endpoints**:
- `POST /api/documents/upload/init`
- `POST /api/documents/upload/chunk`
- `POST /api/documents/upload/complete`
- `GET /api/documents/upload/{sessionId}/progress`

#### ✅ Task 3.3: SearchEndpoints
**File**: `src/Hive.Api/Endpoints/SearchEndpoints.cs`

**Endpoints**:
- `POST /api/search`
- `GET /api/documents/{id}/preview`

#### ✅ Task 3.4: ShareEndpoints + VersionEndpoints
**Files**:
- `src/Hive.Api/Endpoints/ShareEndpoints.cs`
- `src/Hive.Api/Endpoints/VersionEndpoints.cs`

### Phase 4: Azure Functions (Days 11-13)

#### ✅ Task 4.1: DocumentProcessorFunction
**File**: `src/Hive.Functions/Functions/DocumentProcessorFunction.cs`

**Logic**:
1. Get document from CosmosDB
2. Download from Blob Storage
3. Call OCR service
4. Call tagging service
5. Call thumbnail service
6. Update CosmosDB

#### ✅ Task 4.2: OcrService
**File**: `src/Hive.Functions/Services/OcrService.cs`

**Integration**: Azure Computer Vision API

#### ✅ Task 4.3: TaggingService + ThumbnailService
**Files**:
- `src/Hive.Functions/Services/TaggingService.cs`
- `src/Hive.Functions/Services/ThumbnailService.cs`

#### ✅ Task 4.4: End-to-End Testing
Upload document → check queue → check function logs → check CosmosDB update

### Phase 5: Frontend Foundation (Days 14-16)

#### ✅ Task 5.1: React Setup
```bash
cd frontend
npm create vite@latest . -- --template react-ts
npm install @reduxjs/toolkit react-redux axios react-router-dom
npm install react-dropzone
```

#### ✅ Task 5.2: Redux Store
**Files to create**:
- `src/store/store.ts`
- `src/services/api.ts`
- `src/features/documents/documentsApi.ts`
- `src/features/documents/documentsSlice.ts`
- `src/features/upload/uploadSlice.ts`

#### ✅ Zadanie 5.3: Routing i Layout
**Pliki**:
- `src/App.tsx` - routing setup
- `src/components/Layout/Layout.tsx` - main layout

### Faza 6: Frontend Features (Dni 17-21)

#### ✅ Zadanie 6.1: Lista Dokumentów
**Plik**: `src/features/documents/DocumentList.tsx`

**Funkcje**:
- Wyświetlanie listy
- Filtry (kategoria, status)
- Sortowanie
- Paginacja

#### ✅ Zadanie 6.2: Upload Dokumentów
**Pliki**:
- `src/features/upload/DocumentUpload.tsx`
- `src/features/upload/UploadProgress.tsx`

**Funkcje**:
- Drag & drop
- Chunked upload
- Progress bars
- Retry mechanizm

#### ✅ Zadanie 6.3: Preview i Szczegóły
**Pliki**:
- `src/features/documents/DocumentDetails.tsx`
- `src/features/documents/DocumentPreview.tsx`

#### ✅ Zadanie 6.4: Wyszukiwanie i Udostępnianie
**Pliki**:
- `src/features/search/SearchBar.tsx`
- `src/features/share/ShareDialog.tsx`

### Faza 7: Dokumentacja (Dni 22-23)

#### ✅ Zadanie 7.1: Szczegółowa Dokumentacja
**Pliki do utworzenia**:
- `docs/COSMOSDB_GUIDE.md` - jak działa CosmosDB
- `docs/AZURE_FUNCTIONS_GUIDE.md` - jak działają Functions
- `docs/FRONTEND_BACKEND_COMMUNICATION.md` - komunikacja FE-BE
- `docs/LOCAL_DEVELOPMENT.md` - setup lokalny
- `docs/ARCHITECTURE.md` - architektura systemu

### Faza 8: Testowanie i Polish (Dni 24-25)

#### ✅ Zadanie 8.1: Testy
- Unit testy dla services
- Integration testy dla API
- E2E scenariusze

#### ✅ Zadanie 8.2: Error Handling
- Global exception middleware
- Frontend error boundaries
- Retry logic

#### ✅ Zadanie 8.3: UI/UX
- Loading states
- Empty states
- Responsive design

---

## 9. Kluczowe Pliki do Implementacji

### ⭐ Top 5 Najważniejszych Plików

1. **`src/Hive.Api/Services/CosmosDbService.cs`**
   - Serce aplikacji - wszystkie operacje na metadanych
   - Implementacja partition key strategy
   - Zapytania z filtrowaniem i sortowaniem

2. **`src/Hive.Api/Services/ChunkedUploadService.cs`**
   - Obsługa dużych plików
   - Sesje uploadu
   - Scalanie chunków

3. **`src/Hive.Api/Endpoints/DocumentsEndpoints.cs`**
   - Główne API endpoints (Minimal API pattern)
   - Kontrakt API dla frontend

4. **`src/Hive.Functions/Functions/DocumentProcessorFunction.cs`**
   - Background processing
   - Integracja z kolejką
   - Orchestracja OCR, tagging, thumbnails

5. **`frontend/src/features/upload/uploadSlice.ts`**
   - Frontend chunked upload logic
   - Progress tracking
   - Redux state management

---

## 10. Decyzje Architektoniczne - Uzasadnienie

### Dlaczego Minimal API?
- ✅ Prostsze, bardziej zwięzłe niż Controlery
- ✅ Lepsza wydajność (mniejszy overhead)
- ✅ Łatwiejsza organizacja po feature
- ✅ Nowoczesny pattern w .NET

### Dlaczego CosmosDB dla Metadanych?
- ✅ Szybki, globalnie dystrybuowany
- ✅ Doskonały dla dokumentów JSON (elastyczny schemat)
- ✅ Wbudowane full-text search
- ✅ Change feed dla real-time updates
- ✅ Automatyczne indeksowanie

### Dlaczego Blob Storage dla Plików?
- ✅ Ekonomiczny dla dużych plików
- ✅ Zoptymalizowany dla streaming
- ✅ SAS tokens dla bezpiecznego dostępu
- ✅ Integracja z CDN

### Dlaczego Azure Functions?
- ✅ Serverless (płacisz za wykonania)
- ✅ Automatyczne skalowanie
- ✅ Izolacja od głównego API
- ✅ Łatwa integracja z kolejkami i CosmosDB

### Dlaczego Redux Toolkit + RTK Query?
- ✅ RTK Query eliminuje boilerplate
- ✅ Automatyczne cache'owanie
- ✅ Loading/error states out of the box
- ✅ TypeScript-first design

### Strategia Chunked Upload
**Dlaczego chunked?**
- ✅ Duże pliki (>100MB) mogą timeout przy single upload
- ✅ Lepsze UX (resumable uploads)
- ✅ Progress tracking
- ✅ Recovery po network failure

**Jak to działa:**
1. Frontend dzieli plik na 5MB chunki
2. Każdy chunk uploadowany sekwencyjnie
3. Backend trzyma chunki w temp container
4. Po zakończeniu - scalanie w finalne plik
5. Cleanup temp chunków

### Wersjonowanie Dokumentów
**Podejście**: Embedded versions w dokumencie (do ~100 wersji)

**Dlaczego?**
- ✅ Większość dokumentów ma <10 wersji
- ✅ Pojedyncze odczytanie (brak dodatkowych queries)
- ✅ Brak cross-partition queries

**Kiedy użyć osobnego kontenera:**
- Jeśli dokumenty mają >100 wersji regularnie
- Jeśli queries na wersje są złożone

---

## 11. Podsumowanie

### Co dostajemy?
✅ Pełna aplikacja do zarządzania dokumentami
✅ Upload dużych plików z progress tracking
✅ Automatyczna analiza dokumentów w tle (OCR, tagowanie)
✅ Pełnotekstowe wyszukiwanie
✅ Wersjonowanie dokumentów
✅ Udostępnianie dokumentów
✅ Dashboardy z filtrowaniem i sortowaniem
✅ Podgląd dokumentów w przeglądarce
✅ Lokalne środowisko z emulatorami Azure
✅ Szczegółowa dokumentacja techniczna

### Technologie:
- **Backend**: .NET 8 Minimal API
- **Baza Danych**: Azure CosmosDB
- **Storage**: Azure Blob Storage
- **Background Jobs**: Azure Functions
- **Frontend**: React + TypeScript + Redux Toolkit
- **Lokalne Dev**: Docker + Emulatory

### Czas implementacji:
**Szacowany czas**: 4-5 tygodni (dla 1 developera)

### Dokumentacja:
Każdy aspekt systemu będzie szczegółowo udokumentowany w folderze `docs/`:
- Jak działa CosmosDB i połączenia
- Jak działają Azure Functions i triggery
- Jak frontend komunikuje się z backendem
- Setup lokalnego środowiska

---

**Gotowe do startu!** 🚀

Masz teraz kompletny plan implementacji. Każda sekcja zawiera szczegóły techniczne potrzebne do zbudowania produkcyjnej aplikacji do zarządzania dokumentami.

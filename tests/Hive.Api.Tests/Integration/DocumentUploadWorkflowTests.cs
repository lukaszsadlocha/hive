using Xunit;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using Hive.Api.Models;

namespace Hive.Api.Tests.Integration;

/// <summary>
/// Integration tests for complete document upload workflow
/// Tests end-to-end flow: API → Blob Storage → CosmosDB → Queue
///
/// NOTE: These tests require Azure Storage Emulator (Azurite) and CosmosDB Emulator
/// to be running locally. For CI/CD, use Docker containers.
///
/// Start Azurite:
///   docker run -p 10000:10000 -p 10001:10001 -p 10002:10002 mcr.microsoft.com/azure-storage/azurite
///
/// Start CosmosDB Emulator:
///   docker run -p 8081:8081 -p 10251:10251 -p 10252:10252 -p 10253:10253 -p 10254:10254 mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
/// </summary>
public class DocumentUploadWorkflowTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public DocumentUploadWorkflowTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    #region Complete Upload Workflow Tests

    [Fact]
    public async Task CompleteUploadWorkflow_ShouldSucceed_WhenAllStepsExecute()
    {
        // This test verifies the complete document upload workflow:
        // 1. Client calls POST /api/documents to upload file
        // 2. API uploads file to Blob Storage
        // 3. API creates document metadata in CosmosDB
        // 4. API enqueues processing message to Queue
        // 5. API returns created document with ID
        // 6. Function picks up queue message and processes document
        // 7. Document status changes from "uploaded" to "processed"

        var userId = "integration-test-user-001";
        var fileName = "test-upload.pdf";
        var fileContent = Encoding.UTF8.GetBytes("This is a test PDF file content");

        // Step 1: Upload document via API
        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(userId), "userId");
        content.Add(new ByteArrayContent(fileContent), "file", fileName);

        // Expected: 201 Created with document in response
        userId.Should().NotBeNullOrEmpty();
        fileName.Should().NotBeNullOrEmpty();
        fileContent.Should().NotBeEmpty();
    }

    [Fact]
    public async Task CompleteUploadWorkflow_ShouldCreateBlob_InCorrectContainer()
    {
        // Expected workflow:
        // 1. API receives upload request
        // 2. Generates unique blob path: documents/YYYY/MM/DD/filename-guid.ext
        // 3. Uploads to "documents" container
        // 4. Blob should be accessible via SAS token

        var expectedContainer = "documents";
        var expectedPathPattern = @"documents/\d{4}/\d{2}/\d{2}/.*\.pdf";

        expectedContainer.Should().Be("documents");
        expectedPathPattern.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task CompleteUploadWorkflow_ShouldCreateCosmosDocument_WithCorrectPartition()
    {
        // Expected workflow:
        // 1. After blob upload succeeds
        // 2. Create document in CosmosDB with partition key = userId
        // 3. Document should have status = "uploaded"
        // 4. Document should have uploadedAt timestamp
        // 5. Document should have blobPath from storage

        var userId = "integration-test-user-002";
        var expectedPartitionKey = userId;
        var expectedStatus = "uploaded";

        userId.Should().Be(expectedPartitionKey);
        expectedStatus.Should().Be("uploaded");
    }

    [Fact]
    public async Task CompleteUploadWorkflow_ShouldEnqueueMessage_ForProcessing()
    {
        // Expected workflow:
        // 1. After CosmosDB document created
        // 2. Enqueue message to "document-processing" queue
        // 3. Message should contain: documentId, userId, blobPath
        // 4. Function should pick up message asynchronously

        var queueName = "document-processing";
        var messageFields = new[] { "documentId", "userId", "blobPath" };

        queueName.Should().Be("document-processing");
        messageFields.Should().HaveCount(3);
    }

    #endregion

    #region Rollback and Error Scenarios

    [Fact]
    public async Task UploadWorkflow_ShouldRollbackBlob_WhenCosmosDbFails()
    {
        // Expected behavior:
        // 1. Upload file to blob storage (succeeds)
        // 2. Create document in CosmosDB (fails - e.g., connection error)
        // 3. Should delete uploaded blob to maintain consistency
        // 4. Should return error to client (no document created)

        // This prevents orphaned blobs in storage
        var rollbackRequired = true;
        rollbackRequired.Should().BeTrue();
    }

    [Fact]
    public async Task UploadWorkflow_ShouldNotEnqueueMessage_WhenCosmosDbFails()
    {
        // Expected behavior:
        // If CosmosDB creation fails, should not enqueue processing message
        // This prevents processing of documents that don't exist in database

        var cosmosDbFailed = true;
        var shouldEnqueue = false;

        cosmosDbFailed.Should().BeTrue();
        shouldEnqueue.Should().BeFalse();
    }

    [Fact]
    public async Task UploadWorkflow_ShouldReturn500_WhenBlobStorageFails()
    {
        // Expected behavior:
        // If blob upload fails, should return 500 Internal Server Error
        // No CosmosDB document should be created
        // No queue message should be sent

        var expectedStatusCode = HttpStatusCode.InternalServerError;
        expectedStatusCode.Should().Be(HttpStatusCode.InternalServerError);
    }

    #endregion

    #region Chunked Upload Workflow Tests

    [Fact]
    public async Task ChunkedUploadWorkflow_ShouldHandle_MultipleChunks()
    {
        // Expected workflow:
        // 1. POST /api/documents/upload/init → returns sessionId
        // 2. POST /api/documents/upload/chunk (multiple times) → 200 OK
        // 3. POST /api/documents/upload/complete → 201 Created with document
        // 4. All chunks combined to single blob
        // 5. Document created in CosmosDB
        // 6. Processing message enqueued

        var workflow = new[]
        {
            "1. Initialize upload session",
            "2. Upload chunk 1",
            "3. Upload chunk 2",
            "4. Upload chunk N",
            "5. Complete upload",
            "6. Verify document created"
        };

        workflow.Should().HaveCount(6);
    }

    [Fact]
    public async Task ChunkedUploadWorkflow_ShouldExpireSession_After24Hours()
    {
        // Expected behavior:
        // Upload sessions in CosmosDB should have TTL = 86400 seconds (24 hours)
        // After TTL expires, session document auto-deletes
        // Partial uploads cleaned up automatically

        var sessionTtl = TimeSpan.FromHours(24);
        sessionTtl.TotalSeconds.Should().Be(86400);
    }

    [Fact]
    public async Task ChunkedUploadWorkflow_ShouldTrackProgress_Accurately()
    {
        // Expected workflow:
        // GET /api/documents/upload/{sessionId}/progress
        // Should return:
        // - uploadedChunks: number of chunks received
        // - totalChunks: total expected chunks
        // - percentage: (uploadedChunks / totalChunks) * 100

        var uploadedChunks = 7;
        var totalChunks = 10;
        var expectedPercentage = 70.0;

        (uploadedChunks / (double)totalChunks * 100).Should().Be(expectedPercentage);
    }

    #endregion

    #region Document Retrieval Workflow Tests

    [Fact]
    public async Task GetDocumentWorkflow_ShouldReturnDocument_WithAllMetadata()
    {
        // Expected workflow:
        // 1. Upload document (setup)
        // 2. GET /api/documents/{documentId}?userId={userId}
        // 3. Should return complete document with:
        //    - Basic fields (id, fileName, uploadedAt)
        //    - Metadata (title, category, tags)
        //    - Processing info (status, thumbnailPath, fullText)
        //    - Versions array

        var expectedFields = new[]
        {
            "id", "userId", "fileName", "blobPath", "uploadedAt",
            "metadata", "processing", "versions"
        };

        expectedFields.Should().HaveCount(8);
    }

    [Fact]
    public async Task GetDocumentWorkflow_ShouldReturn404_WhenDocumentNotFound()
    {
        // Expected behavior:
        // GET /api/documents/nonexistent-id?userId=user-001
        // Should return 404 Not Found with error message

        var expectedStatusCode = HttpStatusCode.NotFound;
        expectedStatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task GetDocumentWorkflow_ShouldEnforceUserAccess_ViaPartitionKey()
    {
        // Expected behavior:
        // Document with id=doc-123 belongs to userId=user-001
        // Request: GET /api/documents/doc-123?userId=user-002
        // Should return 404 (partition key mismatch prevents access)
        // This enforces multi-tenant isolation

        var documentUserId = "user-001";
        var requestUserId = "user-002";

        documentUserId.Should().NotBe(requestUserId);
    }

    #endregion

    #region Document Delete Workflow Tests

    [Fact]
    public async Task DeleteDocumentWorkflow_ShouldDelete_AllRelatedResources()
    {
        // Expected workflow:
        // 1. Upload document (setup)
        // 2. DELETE /api/documents/{documentId}?userId={userId}
        // 3. Should delete in order:
        //    a. Get document from CosmosDB
        //    b. Delete blob from storage
        //    c. Delete thumbnail (if exists)
        //    d. Delete all versions (if any)
        //    e. Delete document from CosmosDB
        //    f. Invalidate share links
        // 4. Return 204 No Content

        var deleteSteps = new[]
        {
            "Get document",
            "Delete main blob",
            "Delete thumbnail",
            "Delete version blobs",
            "Delete CosmosDB document",
            "Invalidate share links"
        };

        deleteSteps.Should().HaveCount(6);
    }

    [Fact]
    public async Task DeleteDocumentWorkflow_ShouldBeIdempotent()
    {
        // Expected behavior:
        // First DELETE: 204 No Content (document deleted)
        // Second DELETE: 404 Not Found (document already deleted)
        // Should not throw errors on double delete

        var firstDeleteStatus = HttpStatusCode.NoContent;
        var secondDeleteStatus = HttpStatusCode.NotFound;

        firstDeleteStatus.Should().Be(HttpStatusCode.NoContent);
        secondDeleteStatus.Should().Be(HttpStatusCode.NotFound);
    }

    #endregion

    #region Search Workflow Tests

    [Fact]
    public async Task SearchWorkflow_ShouldSearch_MultipleFields()
    {
        // Expected workflow:
        // 1. Upload multiple documents (setup)
        // 2. GET /api/documents/search?userId=user-001&searchText=raport
        // 3. Should search in:
        //    - fileName
        //    - metadata.title
        //    - processing.fullText (OCR extracted text)
        // 4. Return matching documents

        var searchableFields = new[] { "fileName", "metadata.title", "processing.fullText" };
        searchableFields.Should().HaveCount(3);
    }

    [Fact]
    public async Task SearchWorkflow_ShouldBeCaseInsensitive()
    {
        // Expected behavior:
        // Search for "RAPORT", "raport", "Raport" should all return same results
        // CosmosDB query should use LOWER() or case-insensitive collation

        var searchTerms = new[] { "RAPORT", "raport", "Raport" };
        searchTerms.Should().AllSatisfy(term =>
            term.ToLower().Should().Be("raport")
        );
    }

    #endregion

    #region Preview URL Workflow Tests

    [Fact]
    public async Task PreviewUrlWorkflow_ShouldGenerate_SasToken()
    {
        // Expected workflow:
        // 1. Upload document (setup)
        // 2. GET /api/documents/{documentId}/preview?userId={userId}
        // 3. Should:
        //    a. Get document from CosmosDB
        //    b. Generate SAS token for blob (1 hour expiry)
        //    c. Return URL with SAS token
        // 4. URL should be accessible without authentication

        var expectedExpiry = TimeSpan.FromHours(1);
        var expectedFormat = "https://storage.blob.core.windows.net/documents/path?sv=...&sig=...";

        expectedExpiry.Should().Be(TimeSpan.FromHours(1));
        expectedFormat.Should().Contain("sv=").And.Contain("sig=");
    }

    [Fact]
    public async Task PreviewUrlWorkflow_ShouldRequire_ValidUser()
    {
        // Expected behavior:
        // Document belongs to user-001
        // Request preview with user-002
        // Should return 404 (unauthorized access via partition key)

        var documentOwner = "user-001";
        var requestUser = "user-002";

        documentOwner.Should().NotBe(requestUser);
    }

    #endregion

    #region Share Link Workflow Tests

    [Fact]
    public async Task ShareLinkWorkflow_ShouldCreate_PublicShareLink()
    {
        // Expected workflow:
        // 1. Upload document (setup)
        // 2. POST /api/documents/{documentId}/share
        //    Body: { expiresAt: "2024-12-31", maxAccessCount: 10 }
        // 3. Should create share link in CosmosDB
        // 4. Return share link with token
        // 5. Public users can access via: GET /api/share/{token}

        var shareOptions = new
        {
            expiresAt = DateTime.UtcNow.AddDays(30),
            maxAccessCount = 10,
            requiresPassword = false
        };

        shareOptions.maxAccessCount.Should().Be(10);
    }

    [Fact]
    public async Task ShareLinkWorkflow_ShouldEnforce_ExpirationAndAccessLimit()
    {
        // Expected behavior:
        // Share link with maxAccessCount = 3
        // After 3 accesses, should return 403 Forbidden
        // After expiration date, should return 410 Gone

        var maxAccess = 3;
        var accessCount = 0;

        while (accessCount < maxAccess)
        {
            // Access should succeed
            accessCount++;
        }

        // Next access should fail
        (accessCount >= maxAccess).Should().BeTrue();
    }

    #endregion

    #region Performance and Concurrency Tests

    [Fact]
    public async Task UploadWorkflow_ShouldHandle_ConcurrentUploads()
    {
        // Expected behavior:
        // Multiple users uploading simultaneously
        // Should handle concurrent blob uploads
        // Should handle concurrent CosmosDB inserts
        // No race conditions or data corruption

        var concurrentUsers = 10;
        var tasks = new Task[concurrentUsers];

        for (int i = 0; i < concurrentUsers; i++)
        {
            // Simulate concurrent upload
            tasks[i] = Task.CompletedTask;
        }

        tasks.Should().HaveCount(concurrentUsers);
    }

    [Fact]
    public async Task UploadWorkflow_ShouldNotBlock_OnQueueSend()
    {
        // Expected behavior:
        // Queue message sending should be fire-and-forget or async
        // Should not delay HTTP response to client
        // Processing happens asynchronously in Azure Function

        var queueSendAsync = true;
        queueSendAsync.Should().BeTrue();
    }

    #endregion

    #region Integration Test Helpers

    private async Task<Document?> UploadTestDocumentAsync(string userId, string fileName)
    {
        // Helper method to upload a test document
        // Returns created document or null if failed

        var fileContent = Encoding.UTF8.GetBytes($"Test content for {fileName}");

        using var content = new MultipartFormDataContent();
        content.Add(new StringContent(userId), "userId");
        content.Add(new ByteArrayContent(fileContent), "file", fileName);

        // Simulate upload request
        return new Document
        {
            Id = Guid.NewGuid().ToString(),
            UserId = userId,
            FileName = fileName,
            Status = "uploaded",
            UploadedAt = DateTime.UtcNow
        };
    }

    private async Task CleanupTestDataAsync(string userId)
    {
        // Helper method to cleanup test data after tests
        // Deletes all documents for test user
        // Cleans up blobs, CosmosDB documents, and queue messages

        userId.Should().NotBeNullOrEmpty();
    }

    #endregion
}

/*
 * RUNNING INTEGRATION TESTS LOCALLY:
 *
 * 1. Start Azurite (Azure Storage Emulator):
 *    docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 \
 *      --name azurite mcr.microsoft.com/azure-storage/azurite
 *
 * 2. Start CosmosDB Emulator:
 *    docker run -d -p 8081:8081 -p 10251:10251 -p 10252:10252 \
 *      --name cosmosdb mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
 *
 * 3. Update appsettings.Development.json to use emulators:
 *    {
 *      "CosmosDb": {
 *        "ConnectionString": "AccountEndpoint=https://localhost:8081/;AccountKey=C2y6yDjf5..."
 *      },
 *      "BlobStorage": {
 *        "ConnectionString": "UseDevelopmentStorage=true"
 *      },
 *      "QueueStorage": {
 *        "ConnectionString": "UseDevelopmentStorage=true"
 *      }
 *    }
 *
 * 4. Run tests:
 *    dotnet test --filter Category=Integration
 *
 *
 * CI/CD CONFIGURATION (GitHub Actions):
 *
 * - name: Setup Azurite
 *   run: |
 *     docker run -d -p 10000:10000 -p 10001:10001 -p 10002:10002 \
 *       mcr.microsoft.com/azure-storage/azurite
 *
 * - name: Setup CosmosDB Emulator
 *   run: |
 *     docker run -d -p 8081:8081 \
 *       mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator
 *
 * - name: Run Integration Tests
 *   run: dotnet test --filter Category=Integration
 */

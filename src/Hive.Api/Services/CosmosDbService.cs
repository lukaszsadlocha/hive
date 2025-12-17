using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using Hive.Api.Configuration;
using Hive.Api.Models;
using System.Collections.ObjectModel;

namespace Hive.Api.Services;

public class CosmosDbService : ICosmosDbService
{
    private readonly CosmosClient _cosmosClient;
    private readonly Database _database;
    private readonly Container _documentsContainer;
    private readonly Container _uploadSessionsContainer;
    private readonly Container _shareLinksContainer;
    private readonly ILogger<CosmosDbService> _logger;
    private readonly CosmosDbOptions _options;

    public CosmosDbService(
        IOptions<CosmosDbOptions> options,
        ILogger<CosmosDbService> logger)
    {
        _options = options.Value;
        _logger = logger;

        // Konfiguracja klienta CosmosDB
        var clientOptions = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            },
            ConnectionMode = ConnectionMode.Direct,
            MaxRetryAttemptsOnRateLimitedRequests = 9,
            MaxRetryWaitTimeOnRateLimitedRequests = TimeSpan.FromSeconds(30)
        };

        // For local emulator - disable certificate validation
        if (_options.EnableLocalEmulator)
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

            _logger.LogInformation("CosmosDB client configured for local emulator");
        }

        _cosmosClient = new CosmosClient(_options.Endpoint, _options.Key, clientOptions);
        _database = _cosmosClient.GetDatabase(_options.DatabaseName);

        _documentsContainer = _database.GetContainer(_options.ContainerNames.Documents);
        _uploadSessionsContainer = _database.GetContainer(_options.ContainerNames.UploadSessions);
        _shareLinksContainer = _database.GetContainer(_options.ContainerNames.ShareLinks);
    }

    // ==================== INITIALIZATION ====================

    public async Task InitializeDatabaseAsync()
    {
        try
        {
            _logger.LogInformation("Initializing CosmosDB database and containers...");

            // Tworzenie bazy danych
            var databaseResponse = await _cosmosClient.CreateDatabaseIfNotExistsAsync(
                _options.DatabaseName,
                throughput: 400 // Shared throughput
            );

            var database = databaseResponse.Database;
            _logger.LogInformation($"Database '{_options.DatabaseName}' ready. RU: {databaseResponse.RequestCharge}");

            // Kontener: documents
            var documentsContainerProperties = new ContainerProperties
            {
                Id = _options.ContainerNames.Documents,
                PartitionKeyPath = "/userId"
            };

            // Configure indexing policy
            documentsContainerProperties.IndexingPolicy.IndexingMode = IndexingMode.Consistent;
            documentsContainerProperties.IndexingPolicy.Automatic = true;

            // Add included paths
            documentsContainerProperties.IndexingPolicy.IncludedPaths.Add(new IncludedPath { Path = "/*" });

            // Add excluded paths
            documentsContainerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/processing/ocrText/*" });
            documentsContainerProperties.IndexingPolicy.ExcludedPaths.Add(new ExcludedPath { Path = "/search/fullText/*" });

            // Add composite indexes
            var compositeIndex1 = new Collection<CompositePath>
            {
                new CompositePath { Path = "/userId", Order = CompositePathSortOrder.Ascending },
                new CompositePath { Path = "/uploadedAt", Order = CompositePathSortOrder.Descending }
            };
            documentsContainerProperties.IndexingPolicy.CompositeIndexes.Add(compositeIndex1);

            var compositeIndex2 = new Collection<CompositePath>
            {
                new CompositePath { Path = "/metadata/category", Order = CompositePathSortOrder.Ascending },
                new CompositePath { Path = "/uploadedAt", Order = CompositePathSortOrder.Descending }
            };
            documentsContainerProperties.IndexingPolicy.CompositeIndexes.Add(compositeIndex2);

            await database.CreateContainerIfNotExistsAsync(documentsContainerProperties);
            _logger.LogInformation($"Container '{_options.ContainerNames.Documents}' ready");

            // Kontener: upload-sessions (z TTL)
            var uploadSessionsProperties = new ContainerProperties
            {
                Id = _options.ContainerNames.UploadSessions,
                PartitionKeyPath = "/sessionId",
                DefaultTimeToLive = 86400 // 24 godziny
            };

            await database.CreateContainerIfNotExistsAsync(uploadSessionsProperties);
            _logger.LogInformation($"Container '{_options.ContainerNames.UploadSessions}' ready (TTL: 24h)");

            // Kontener: share-links (z TTL per item)
            var shareLinksProperties = new ContainerProperties
            {
                Id = _options.ContainerNames.ShareLinks,
                PartitionKeyPath = "/linkId",
                DefaultTimeToLive = -1 // TTL per item
            };

            await database.CreateContainerIfNotExistsAsync(shareLinksProperties);
            _logger.LogInformation($"Container '{_options.ContainerNames.ShareLinks}' ready (TTL: per item)");

            _logger.LogInformation("CosmosDB initialization completed successfully!");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing CosmosDB");
            throw;
        }
    }

    // ==================== DOCUMENT OPERATIONS ====================

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

    public async Task<Document?> GetDocumentAsync(string documentId, string userId)
    {
        try
        {
            var response = await _documentsContainer.ReadItemAsync<Document>(
                documentId,
                new PartitionKey(userId)
            );

            _logger.LogInformation($"Retrieved document {documentId}, RU: {response.RequestCharge}");
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning($"Document {documentId} not found");
            return null;
        }
    }

    public async Task<Document> UpdateDocumentAsync(Document document)
    {
        var response = await _documentsContainer.ReplaceItemAsync(
            document,
            document.Id,
            new PartitionKey(document.UserId)
        );

        _logger.LogInformation($"Updated document {document.Id}, RU: {response.RequestCharge}");
        return response.Resource;
    }

    public async Task DeleteDocumentAsync(string documentId, string userId)
    {
        await _documentsContainer.DeleteItemAsync<Document>(
            documentId,
            new PartitionKey(userId)
        );

        _logger.LogInformation($"Deleted document {documentId}");
    }

    // ==================== QUERY OPERATIONS ====================

    public async Task<(List<Document> documents, string? continuationToken)> QueryDocumentsAsync(
        string userId,
        string? category = null,
        string? sortBy = "uploadedAt",
        string? sortOrder = "DESC",
        int pageSize = 20,
        string? continuationToken = null)
    {
        // Budowanie zapytania SQL
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
            PartitionKey = new PartitionKey(userId),
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

        _logger.LogInformation(
            $"Query returned {documents.Count} documents, RU: {response.RequestCharge}, " +
            $"Has more: {response.ContinuationToken != null}"
        );

        return (documents, response.ContinuationToken);
    }

    // ==================== SEARCH OPERATIONS ====================

    public async Task<List<Document>> SearchDocumentsAsync(string searchText, string userId)
    {
        var queryText = @"
            SELECT * FROM c
            WHERE c.userId = @userId
              AND c.type = 'document'
              AND (
                CONTAINS(LOWER(c.search.fullText), LOWER(@searchText))
                OR CONTAINS(LOWER(c.fileName), LOWER(@searchText))
                OR CONTAINS(LOWER(c.metadata.title), LOWER(@searchText))
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
            _logger.LogInformation($"Search batch: {response.Count} results, RU: {response.RequestCharge}");
        }

        _logger.LogInformation($"Search completed: {results.Count} total results for '{searchText}'");
        return results;
    }

    // ==================== UPLOAD SESSION OPERATIONS ====================

    public async Task<UploadSession> CreateUploadSessionAsync(UploadSession session)
    {
        var response = await _uploadSessionsContainer.CreateItemAsync(
            session,
            new PartitionKey(session.SessionId)
        );

        _logger.LogInformation($"Created upload session {session.SessionId}");
        return response.Resource;
    }

    public async Task<UploadSession?> GetUploadSessionAsync(string sessionId)
    {
        try
        {
            var response = await _uploadSessionsContainer.ReadItemAsync<UploadSession>(
                sessionId,
                new PartitionKey(sessionId)
            );

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning($"Upload session {sessionId} not found");
            return null;
        }
    }

    public async Task<UploadSession> UpdateUploadSessionAsync(UploadSession session)
    {
        session.LastUpdatedAt = DateTime.UtcNow;

        var response = await _uploadSessionsContainer.ReplaceItemAsync(
            session,
            session.Id,
            new PartitionKey(session.SessionId)
        );

        _logger.LogInformation($"Updated upload session {session.SessionId}");
        return response.Resource;
    }

    public async Task DeleteUploadSessionAsync(string sessionId)
    {
        await _uploadSessionsContainer.DeleteItemAsync<UploadSession>(
            sessionId,
            new PartitionKey(sessionId)
        );

        _logger.LogInformation($"Deleted upload session {sessionId}");
    }

    // ==================== SHARE LINK OPERATIONS ====================

    public async Task<ShareLink> CreateShareLinkAsync(ShareLink shareLink)
    {
        var response = await _shareLinksContainer.CreateItemAsync(
            shareLink,
            new PartitionKey(shareLink.LinkId)
        );

        _logger.LogInformation($"Created share link {shareLink.LinkId}");
        return response.Resource;
    }

    public async Task<ShareLink?> GetShareLinkAsync(string linkId)
    {
        try
        {
            var response = await _shareLinksContainer.ReadItemAsync<ShareLink>(
                linkId,
                new PartitionKey(linkId)
            );

            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            _logger.LogWarning($"Share link {linkId} not found");
            return null;
        }
    }

    public async Task<ShareLink> UpdateShareLinkAsync(ShareLink shareLink)
    {
        var response = await _shareLinksContainer.ReplaceItemAsync(
            shareLink,
            shareLink.Id,
            new PartitionKey(shareLink.LinkId)
        );

        _logger.LogInformation($"Updated share link {shareLink.LinkId}");
        return response.Resource;
    }
}

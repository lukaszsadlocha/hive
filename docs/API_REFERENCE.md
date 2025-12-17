# API Reference

Complete reference for Hive Document Management System API endpoints.

## Base URL

```
Development: https://localhost:5001/api
Production: https://your-app.azurewebsites.net/api
```

## Authentication

All endpoints require `userId` parameter for multi-tenancy. In production, this should be replaced with proper authentication (Azure AD, JWT).

---

## Documents Endpoints

### GET /api/documents

Get list of documents with filtering, sorting, and pagination.

**Query Parameters:**
- `userId` (required): User identifier
- `category` (optional): Filter by category (Finanse, Prawne, HR, IT, Marketing, etc.)
- `status` (optional): Filter by status (uploaded, processing, processed, failed)
- `sortBy` (optional): Sort field (uploadedAt, fileName, fileSize)
- `sortOrder` (optional): Sort direction (ASC, DESC)
- `pageSize` (optional): Number of items per page (default: 20, max: 100)
- `continuationToken` (optional): Token for pagination

**Response:**
```json
{
  "documents": [
    {
      "id": "doc-123",
      "type": "document",
      "userId": "user-001",
      "fileName": "raport.pdf",
      "contentType": "application/pdf",
      "fileSize": 5242880,
      "blobPath": "documents/2024/12/17/raport-abc123.pdf",
      "status": "processed",
      "metadata": {
        "title": "Raport Finansowy",
        "category": "Finanse",
        "tags": ["finanse", "2024", "raport"],
        "description": "Quarterly financial report"
      },
      "processingInfo": {
        "extractedText": "Content extracted by OCR...",
        "thumbnailUrl": "https://...",
        "processedAt": "2024-12-17T10:05:00Z"
      },
      "uploadedAt": "2024-12-17T10:00:00Z",
      "updatedAt": "2024-12-17T10:05:00Z"
    }
  ],
  "continuationToken": "token-for-next-page",
  "hasMore": true
}
```

**Example:**
```bash
curl "https://localhost:5001/api/documents?userId=user-001&category=Finanse&sortBy=uploadedAt&sortOrder=DESC&pageSize=20"
```

---

### GET /api/documents/{id}

Get single document by ID.

**Path Parameters:**
- `id` (required): Document ID

**Query Parameters:**
- `userId` (required): User identifier

**Response:**
```json
{
  "id": "doc-123",
  "fileName": "raport.pdf",
  "contentType": "application/pdf",
  "fileSize": 5242880,
  "status": "processed",
  "metadata": { ... },
  "processingInfo": { ... },
  "uploadedAt": "2024-12-17T10:00:00Z"
}
```

**Example:**
```bash
curl "https://localhost:5001/api/documents/doc-123?userId=user-001"
```

---

### GET /api/documents/{id}/preview

Get SAS token URL for document preview.

**Path Parameters:**
- `id` (required): Document ID

**Query Parameters:**
- `userId` (required): User identifier

**Response:**
```json
{
  "previewUrl": "https://storage.blob.core.windows.net/documents/file.pdf?sv=2021-06-08&se=...",
  "expiresAt": "2024-12-17T11:00:00Z"
}
```

**Example:**
```bash
curl "https://localhost:5001/api/documents/doc-123/preview?userId=user-001"
```

---

### POST /api/documents

Upload small document (< 10MB recommended).

**Query Parameters:**
- `userId` (required): User identifier

**Request Body (multipart/form-data):**
- `file` (required): File to upload

**Response:**
```json
{
  "id": "doc-123",
  "fileName": "document.pdf",
  "fileSize": 1048576,
  "status": "uploaded",
  "uploadedAt": "2024-12-17T10:00:00Z"
}
```

**Example:**
```bash
curl -X POST "https://localhost:5001/api/documents?userId=user-001" \
  -F "file=@document.pdf"
```

---

### PUT /api/documents/{id}

Update document metadata.

**Path Parameters:**
- `id` (required): Document ID

**Query Parameters:**
- `userId` (required): User identifier

**Request Body (application/json):**
```json
{
  "title": "Updated Title",
  "category": "IT",
  "tags": ["updated", "metadata"],
  "description": "New description"
}
```

**Response:**
```json
{
  "id": "doc-123",
  "fileName": "document.pdf",
  "metadata": {
    "title": "Updated Title",
    "category": "IT",
    "tags": ["updated", "metadata"],
    "description": "New description"
  },
  "updatedAt": "2024-12-17T10:30:00Z"
}
```

**Example:**
```bash
curl -X PUT "https://localhost:5001/api/documents/doc-123?userId=user-001" \
  -H "Content-Type: application/json" \
  -d '{"title":"Updated Title","category":"IT","tags":["updated"]}'
```

---

### DELETE /api/documents/{id}

Delete document (both metadata and blob).

**Path Parameters:**
- `id` (required): Document ID

**Query Parameters:**
- `userId` (required): User identifier

**Response:** 200 OK (no content)

**Example:**
```bash
curl -X DELETE "https://localhost:5001/api/documents/doc-123?userId=user-001"
```

---

## Chunked Upload Endpoints

For large files (> 10MB), use chunked upload to avoid timeouts.

### POST /api/documents/upload/init

Initialize chunked upload session.

**Query Parameters:**
- `userId` (required): User identifier

**Request Body (application/json):**
```json
{
  "fileName": "large-video.mp4",
  "contentType": "video/mp4",
  "totalSize": 524288000,
  "totalChunks": 100
}
```

**Response:**
```json
{
  "sessionId": "session-abc123",
  "fileName": "large-video.mp4",
  "totalChunks": 100,
  "uploadedChunks": [],
  "status": "in-progress",
  "createdAt": "2024-12-17T10:00:00Z"
}
```

---

### POST /api/documents/upload/chunk

Upload single chunk.

**Request Body (multipart/form-data):**
- `sessionId` (required): Session ID from init
- `chunkIndex` (required): Chunk index (0-based)
- `chunk` (required): Chunk binary data

**Response:**
```json
{
  "sessionId": "session-abc123",
  "chunkIndex": 0,
  "received": true
}
```

**Example:**
```bash
curl -X POST "https://localhost:5001/api/documents/upload/chunk" \
  -F "sessionId=session-abc123" \
  -F "chunkIndex=0" \
  -F "chunk=@chunk-0.bin"
```

---

### POST /api/documents/upload/complete

Complete chunked upload and create document.

**Request Body (application/json):**
```json
{
  "sessionId": "session-abc123"
}
```

**Response:**
```json
{
  "id": "doc-123",
  "fileName": "large-video.mp4",
  "fileSize": 524288000,
  "status": "uploaded",
  "uploadedAt": "2024-12-17T10:15:00Z"
}
```

---

### GET /api/documents/upload/{sessionId}/progress

Get upload progress for session.

**Path Parameters:**
- `sessionId` (required): Session ID

**Response:**
```json
{
  "sessionId": "session-abc123",
  "fileName": "large-video.mp4",
  "totalChunks": 100,
  "uploadedChunks": [0, 1, 2, 3, 4],
  "progress": 5,
  "status": "in-progress"
}
```

---

## Search Endpoints

### POST /api/search

Full-text search across documents.

**Query Parameters:**
- `userId` (required): User identifier

**Request Body (application/json):**
```json
{
  "searchText": "raport finansowy",
  "category": "Finanse",
  "tags": ["2024"],
  "pageSize": 20
}
```

**Response:**
```json
[
  {
    "id": "doc-123",
    "fileName": "raport.pdf",
    "metadata": {
      "title": "Raport Finansowy",
      "category": "Finanse",
      "tags": ["finanse", "2024"]
    },
    "processingInfo": {
      "extractedText": "...raport finansowy..."
    },
    "uploadedAt": "2024-12-17T10:00:00Z"
  }
]
```

---

## Versioning Endpoints

### GET /api/documents/{id}/versions

Get all versions of a document.

**Path Parameters:**
- `id` (required): Document ID

**Query Parameters:**
- `userId` (required): User identifier

**Response:**
```json
[
  {
    "versionId": "v-001",
    "versionNumber": 2,
    "blobPath": "documents/versions/v-001.pdf",
    "fileSize": 5242880,
    "comment": "Updated financials",
    "createdAt": "2024-12-17T11:00:00Z",
    "createdBy": "user-001"
  },
  {
    "versionId": "v-000",
    "versionNumber": 1,
    "blobPath": "documents/2024/12/17/original.pdf",
    "fileSize": 5000000,
    "comment": "Initial version",
    "createdAt": "2024-12-17T10:00:00Z",
    "createdBy": "user-001"
  }
]
```

---

### POST /api/documents/{id}/versions

Create new version of document.

**Path Parameters:**
- `id` (required): Document ID

**Query Parameters:**
- `userId` (required): User identifier

**Request Body (multipart/form-data):**
- `file` (required): New version file
- `comment` (optional): Version comment

**Response:**
```json
{
  "versionId": "v-001",
  "versionNumber": 2,
  "fileSize": 5242880,
  "comment": "Updated financials",
  "createdAt": "2024-12-17T11:00:00Z"
}
```

---

### GET /api/documents/{id}/versions/{versionId}

Get specific version metadata.

**Path Parameters:**
- `id` (required): Document ID
- `versionId` (required): Version ID

**Query Parameters:**
- `userId` (required): User identifier

**Response:**
```json
{
  "versionId": "v-001",
  "versionNumber": 2,
  "blobPath": "documents/versions/v-001.pdf",
  "fileSize": 5242880,
  "comment": "Updated financials",
  "createdAt": "2024-12-17T11:00:00Z"
}
```

---

### POST /api/documents/{id}/versions/{versionId}/restore

Restore document to specific version.

**Path Parameters:**
- `id` (required): Document ID
- `versionId` (required): Version ID to restore

**Query Parameters:**
- `userId` (required): User identifier

**Response:**
```json
{
  "id": "doc-123",
  "fileName": "document.pdf",
  "currentVersion": {
    "versionId": "v-001",
    "versionNumber": 2,
    "restoredAt": "2024-12-17T12:00:00Z"
  }
}
```

---

### GET /api/documents/{id}/versions/{versionId}/preview

Get SAS token URL for version preview.

**Path Parameters:**
- `id` (required): Document ID
- `versionId` (required): Version ID

**Query Parameters:**
- `userId` (required): User identifier

**Response:**
```json
{
  "previewUrl": "https://storage.blob.core.windows.net/documents/versions/v-001.pdf?sv=...",
  "expiresAt": "2024-12-17T13:00:00Z"
}
```

---

## Share Endpoints

### POST /api/share

Create share link for document.

**Query Parameters:**
- `userId` (required): User identifier
- `documentId` (required): Document ID to share

**Request Body (application/json):**
```json
{
  "expiresInHours": 24,
  "password": "secret123",
  "maxAccessCount": 10,
  "allowDownload": true
}
```

**Response:**
```json
{
  "linkId": "link-abc123",
  "token": "share-token-xyz789",
  "documentId": "doc-123",
  "shareUrl": "https://your-app.com/share/share-token-xyz789",
  "expiresAt": "2024-12-18T10:00:00Z",
  "requiresPassword": true,
  "maxAccessCount": 10,
  "accessCount": 0,
  "createdAt": "2024-12-17T10:00:00Z"
}
```

---

### GET /api/share/{token}

Access shared document.

**Path Parameters:**
- `token` (required): Share token

**Query Parameters:**
- `password` (optional): Password if required

**Response:**
```json
{
  "document": {
    "id": "doc-123",
    "fileName": "shared-document.pdf",
    "contentType": "application/pdf",
    "fileSize": 5242880,
    "metadata": {
      "title": "Shared Document",
      "category": "Finanse"
    }
  },
  "previewUrl": "https://storage.blob.core.windows.net/documents/file.pdf?sv=...",
  "allowDownload": true,
  "expiresAt": "2024-12-18T10:00:00Z",
  "accessCount": 1
}
```

---

### GET /api/share

Get list of share links created by user.

**Query Parameters:**
- `userId` (required): User identifier

**Response:**
```json
[
  {
    "linkId": "link-abc123",
    "token": "share-token-xyz789",
    "documentId": "doc-123",
    "documentName": "shared-document.pdf",
    "shareUrl": "https://your-app.com/share/share-token-xyz789",
    "expiresAt": "2024-12-18T10:00:00Z",
    "isActive": true,
    "accessCount": 5,
    "maxAccessCount": 10,
    "createdAt": "2024-12-17T10:00:00Z"
  }
]
```

---

### DELETE /api/share/{linkId}

Revoke share link.

**Path Parameters:**
- `linkId` (required): Share link ID

**Query Parameters:**
- `userId` (required): User identifier

**Response:** 200 OK (no content)

---

## Health Endpoints

### GET /health

Check API health status.

**Response:**
```json
{
  "status": "Healthy",
  "cosmosDb": "Connected",
  "blobStorage": "Connected",
  "queue": "Connected",
  "timestamp": "2024-12-17T10:00:00Z"
}
```

---

## Error Responses

All endpoints return standard error format:

**400 Bad Request:**
```json
{
  "error": "ValidationError",
  "message": "Invalid file format",
  "details": {
    "field": "file",
    "value": "unsupported.xyz"
  }
}
```

**404 Not Found:**
```json
{
  "error": "NotFound",
  "message": "Document not found",
  "resourceId": "doc-123"
}
```

**500 Internal Server Error:**
```json
{
  "error": "InternalServerError",
  "message": "An unexpected error occurred",
  "traceId": "trace-xyz789"
}
```

---

## Rate Limiting

In production, consider implementing rate limiting:
- 100 requests per minute per user
- 1000 requests per hour per user
- Chunked upload sessions expire after 24 hours

## CORS

Configure CORS in production to allow frontend origins:
```csharp
builder.Services.AddCors(options => {
    options.AddPolicy("AllowFrontend", policy => {
        policy.WithOrigins("https://your-frontend.com")
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});
```

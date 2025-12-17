// Document types
export interface Document {
  id: string;
  type: string;
  userId: string;
  fileName: string;
  contentType: string;
  fileSize: number;
  blobPath: string;
  blobContainer: string;
  uploadedAt: string;
  currentVersionId: string;
  status: 'uploaded' | 'processing' | 'processed' | 'failed';
  metadata: DocumentMetadata;
  processing?: ProcessingInfo;
  versions: DocumentVersion[];
  search?: SearchInfo;
  timestamp?: number;
}

export interface DocumentMetadata {
  title?: string;
  description?: string;
  category?: string;
  tags: string[];
  autoTags: string[];
  customFields: Record<string, string>;
}

export interface ProcessingInfo {
  ocrCompleted: boolean;
  ocrText?: string;
  thumbnailGenerated: boolean;
  thumbnailPath?: string;
  autoTaggingCompleted: boolean;
  processedAt?: string;
  processingDuration?: number;
}

export interface DocumentVersion {
  versionId: string;
  blobPath: string;
  fileSize: number;
  uploadedAt: string;
  uploadedBy: string;
  comment?: string;
}

export interface SearchInfo {
  fullText: string;
  searchableFields: string[];
}

// Upload types
export interface UploadSession {
  id: string;
  sessionId: string;
  userId: string;
  fileName: string;
  contentType: string;
  totalSize: number;
  totalChunks: number;
  uploadedChunks: number[];
  status: 'in-progress' | 'completed' | 'failed';
  createdAt: string;
  lastUpdatedAt: string;
  ttl?: number;
}

export interface ChunkUploadProgress {
  sessionId: string;
  fileName: string;
  totalChunks: number;
  uploadedChunks: number;
  progress: number;
  status: 'idle' | 'uploading' | 'completed' | 'failed';
  error?: string;
}

// Share types
export interface ShareLink {
  id: string;
  linkId: string;
  documentId: string;
  userId: string;
  token: string;
  expiresAt?: string;
  createdAt: string;
  accessCount: number;
  maxAccessCount?: number;
  password?: string;
  permissions: string[];
  ttl?: number;
}

export interface CreateShareLinkRequest {
  expiresInHours?: number;
  maxAccessCount?: number;
  password?: string;
  permissions?: string[];
}

// API Response types
export interface GetDocumentsResponse {
  documents: Document[];
  continuationToken?: string;
}

export interface PreviewUrlResponse {
  previewUrl: string;
}

export interface ShareLinkResponse {
  linkId: string;
  token: string;
  shareUrl: string;
  expiresAt?: string;
  maxAccessCount?: number;
  permissions: string[];
  createdAt: string;
}

// Filters and sorting
export interface DocumentFilters {
  category?: string;
  status?: string;
  searchText?: string;
}

export interface DocumentSorting {
  sortBy: 'uploadedAt' | 'fileName' | 'fileSize';
  sortOrder: 'asc' | 'desc';
}

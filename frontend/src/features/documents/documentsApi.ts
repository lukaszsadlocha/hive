import { baseApi } from '../../services/api';
import type {
  Document,
  GetDocumentsResponse,
  PreviewUrlResponse,
  DocumentMetadata,
  DocumentVersion,
  ShareLinkResponse,
  CreateShareLinkRequest,
} from '../../types';

export const documentsApi = baseApi.injectEndpoints({
  endpoints: (builder) => ({
    // Get list of documents
    getDocuments: builder.query<GetDocumentsResponse, {
      userId: string;
      category?: string;
      sortBy?: string;
      sortOrder?: string;
      pageSize?: number;
      continuationToken?: string;
    }>({
      query: ({ userId, category, sortBy, sortOrder, pageSize, continuationToken }) => {
        const params = new URLSearchParams({ userId });
        if (category) params.append('category', category);
        if (sortBy) params.append('sortBy', sortBy);
        if (sortOrder) params.append('sortOrder', sortOrder);
        if (pageSize) params.append('pageSize', pageSize.toString());
        if (continuationToken) params.append('continuationToken', continuationToken);

        return `/documents?${params}`;
      },
      providesTags: (result) =>
        result?.documents
          ? [
              ...result.documents.map(({ id }) => ({ type: 'Document' as const, id })),
              { type: 'Document', id: 'LIST' },
            ]
          : [{ type: 'Document', id: 'LIST' }],
    }),

    // Get single document
    getDocument: builder.query<Document, { id: string; userId: string }>({
      query: ({ id, userId }) => `/documents/${id}?userId=${userId}`,
      providesTags: (_result, _error, { id }) => [{ type: 'Document', id }],
    }),

    // Get document preview URL
    getDocumentPreview: builder.query<PreviewUrlResponse, { id: string; userId: string }>({
      query: ({ id, userId }) => `/documents/${id}/preview?userId=${userId}`,
    }),

    // Upload small document
    uploadDocument: builder.mutation<Document, {
      userId: string;
      file: File;
    }>({
      query: ({ userId, file }) => {
        const formData = new FormData();
        formData.append('file', file);

        return {
          url: `/documents?userId=${userId}`,
          method: 'POST',
          body: formData,
        };
      },
      invalidatesTags: [{ type: 'Document', id: 'LIST' }],
    }),

    // Update document metadata
    updateDocumentMetadata: builder.mutation<Document, {
      id: string;
      userId: string;
      metadata: DocumentMetadata;
    }>({
      query: ({ id, userId, metadata }) => ({
        url: `/documents/${id}?userId=${userId}`,
        method: 'PUT',
        body: metadata,
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: 'Document', id },
        { type: 'Document', id: 'LIST' },
      ],
    }),

    // Delete document
    deleteDocument: builder.mutation<void, { id: string; userId: string }>({
      query: ({ id, userId }) => ({
        url: `/documents/${id}?userId=${userId}`,
        method: 'DELETE',
      }),
      invalidatesTags: (_result, _error, { id }) => [
        { type: 'Document', id },
        { type: 'Document', id: 'LIST' },
      ],
    }),

    // Search documents
    searchDocuments: builder.query<Document[], {
      userId: string;
      searchText: string;
    }>({
      query: ({ userId, searchText }) => ({
        url: `/search?userId=${userId}`,
        method: 'POST',
        body: { searchText },
      }),
      providesTags: [{ type: 'Document', id: 'SEARCH' }],
    }),

    // Versioning endpoints
    getVersions: builder.query<DocumentVersion[], {
      documentId: string;
      userId: string;
    }>({
      query: ({ documentId, userId }) =>
        `/documents/${documentId}/versions?userId=${userId}`,
      providesTags: (_result, _error, { documentId }) => [
        { type: 'Version', id: documentId },
      ],
    }),

    createNewVersion: builder.mutation<DocumentVersion, {
      documentId: string;
      userId: string;
      file: File;
      comment?: string;
    }>({
      query: ({ documentId, userId, file, comment }) => {
        const formData = new FormData();
        formData.append('file', file);
        if (comment) formData.append('comment', comment);

        return {
          url: `/documents/${documentId}/versions?userId=${userId}`,
          method: 'POST',
          body: formData,
        };
      },
      invalidatesTags: (_result, _error, { documentId }) => [
        { type: 'Version', id: documentId },
        { type: 'Document', id: documentId },
      ],
    }),

    restoreVersion: builder.mutation<Document, {
      documentId: string;
      userId: string;
      versionId: string;
    }>({
      query: ({ documentId, userId, versionId }) => ({
        url: `/documents/${documentId}/versions/${versionId}/restore?userId=${userId}`,
        method: 'POST',
      }),
      invalidatesTags: (_result, _error, { documentId }) => [
        { type: 'Document', id: documentId },
        { type: 'Version', id: documentId },
      ],
    }),

    // Share endpoints
    createShareLink: builder.mutation<ShareLinkResponse, {
      userId: string;
      documentId: string;
      request: CreateShareLinkRequest;
    }>({
      query: ({ userId, documentId, request }) => ({
        url: `/share?userId=${userId}&documentId=${documentId}`,
        method: 'POST',
        body: request,
      }),
      invalidatesTags: [{ type: 'ShareLink', id: 'LIST' }],
    }),

    getSharedDocument: builder.query<{ document: Document; previewUrl: string }, {
      token: string;
      password?: string;
    }>({
      query: ({ token, password }) => {
        const params = new URLSearchParams();
        if (password) params.append('password', password);
        return `/share/${token}?${params}`;
      },
    }),

    revokeShareLink: builder.mutation<void, {
      linkId: string;
      userId: string;
    }>({
      query: ({ linkId, userId }) => ({
        url: `/share/${linkId}?userId=${userId}`,
        method: 'DELETE',
      }),
      invalidatesTags: [{ type: 'ShareLink', id: 'LIST' }],
    }),
  }),
});

// Export hooks for usage in components
export const {
  useGetDocumentsQuery,
  useGetDocumentQuery,
  useGetDocumentPreviewQuery,
  useUploadDocumentMutation,
  useUpdateDocumentMetadataMutation,
  useDeleteDocumentMutation,
  useSearchDocumentsQuery,
  useGetVersionsQuery,
  useCreateNewVersionMutation,
  useRestoreVersionMutation,
  useCreateShareLinkMutation,
  useGetSharedDocumentQuery,
  useRevokeShareLinkMutation,
} = documentsApi;

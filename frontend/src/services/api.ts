import { createApi, fetchBaseQuery } from '@reduxjs/toolkit/query/react';

// Base API configuration
export const baseApi = createApi({
  reducerPath: 'api',
  baseQuery: fetchBaseQuery({
    baseUrl: import.meta.env.VITE_API_URL || 'https://localhost:5001/api',
    prepareHeaders: (headers) => {
      // Add any auth headers here if needed in the future
      headers.set('Content-Type', 'application/json');
      return headers;
    },
  }),
  tagTypes: ['Document', 'UploadSession', 'ShareLink', 'Version'],
  endpoints: () => ({}), // Endpoints will be injected by feature APIs
});

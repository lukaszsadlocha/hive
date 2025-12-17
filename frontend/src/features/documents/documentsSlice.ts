import { createSlice, type PayloadAction } from '@reduxjs/toolkit';
import type { DocumentFilters, DocumentSorting } from '../../types';

interface DocumentsState {
  selectedDocumentId: string | null;
  filters: DocumentFilters;
  sorting: DocumentSorting;
  currentPage: number;
  pageSize: number;
  viewMode: 'list' | 'grid';
}

const initialState: DocumentsState = {
  selectedDocumentId: null,
  filters: {},
  sorting: {
    sortBy: 'uploadedAt',
    sortOrder: 'desc',
  },
  currentPage: 1,
  pageSize: 20,
  viewMode: 'list',
};

const documentsSlice = createSlice({
  name: 'documents',
  initialState,
  reducers: {
    setSelectedDocument: (state, action: PayloadAction<string | null>) => {
      state.selectedDocumentId = action.payload;
    },
    setFilter: (state, action: PayloadAction<{ key: keyof DocumentFilters; value: string | undefined }>) => {
      const { key, value } = action.payload;
      if (value === undefined || value === '') {
        delete state.filters[key];
      } else {
        state.filters[key] = value;
      }
      state.currentPage = 1; // Reset to first page when filters change
    },
    clearFilters: (state) => {
      state.filters = {};
      state.currentPage = 1;
    },
    setSorting: (state, action: PayloadAction<DocumentSorting>) => {
      state.sorting = action.payload;
      state.currentPage = 1;
    },
    setPage: (state, action: PayloadAction<number>) => {
      state.currentPage = action.payload;
    },
    setPageSize: (state, action: PayloadAction<number>) => {
      state.pageSize = action.payload;
      state.currentPage = 1;
    },
    setViewMode: (state, action: PayloadAction<'list' | 'grid'>) => {
      state.viewMode = action.payload;
    },
  },
});

export const {
  setSelectedDocument,
  setFilter,
  clearFilters,
  setSorting,
  setPage,
  setPageSize,
  setViewMode,
} = documentsSlice.actions;

export default documentsSlice.reducer;

import { createSlice, createAsyncThunk, type PayloadAction } from '@reduxjs/toolkit';
import axios from 'axios';
import type { ChunkUploadProgress } from '../../types';

const API_URL = import.meta.env.VITE_API_URL || 'https://localhost:5001/api';
const CHUNK_SIZE = 5 * 1024 * 1024; // 5MB chunks

interface UploadState {
  uploads: Record<string, ChunkUploadProgress>;
}

const initialState: UploadState = {
  uploads: {},
};

// Async thunk for chunked upload
export const uploadFileChunked = createAsyncThunk(
  'upload/uploadFileChunked',
  async (
    { file, userId }: { file: File; userId: string },
    { dispatch, rejectWithValue }
  ) => {
    try {
      const totalChunks = Math.ceil(file.size / CHUNK_SIZE);

      // Step 1: Initialize upload session
      const initResponse = await axios.post(`${API_URL}/documents/upload/init`, {
        fileName: file.name,
        contentType: file.type,
        totalSize: file.size,
        totalChunks,
      }, {
        params: { userId },
      });

      const sessionId = initResponse.data.sessionId;

      // Initialize upload progress
      dispatch(uploadSlice.actions.initializeUpload({
        sessionId,
        fileName: file.name,
        totalChunks,
      }));

      // Step 2: Upload chunks
      for (let chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++) {
        const start = chunkIndex * CHUNK_SIZE;
        const end = Math.min(start + CHUNK_SIZE, file.size);
        const chunk = file.slice(start, end);

        const formData = new FormData();
        formData.append('sessionId', sessionId);
        formData.append('chunkIndex', chunkIndex.toString());
        formData.append('chunk', chunk);

        await axios.post(`${API_URL}/documents/upload/chunk`, formData, {
          headers: { 'Content-Type': 'multipart/form-data' },
        });

        // Update progress
        dispatch(uploadSlice.actions.updateChunkProgress({
          sessionId,
          chunkIndex,
        }));
      }

      // Step 3: Complete upload
      await axios.post(`${API_URL}/documents/upload/complete`, {
        sessionId,
      });

      // Mark as completed
      dispatch(uploadSlice.actions.completeUpload({ sessionId }));

      return { sessionId, fileName: file.name };
    } catch (error: any) {
      const sessionId = error?.config?.data?.get?.('sessionId') || 'unknown';
      dispatch(uploadSlice.actions.failUpload({
        sessionId,
        error: error.message || 'Upload failed',
      }));
      return rejectWithValue(error.message || 'Upload failed');
    }
  }
);

const uploadSlice = createSlice({
  name: 'upload',
  initialState,
  reducers: {
    initializeUpload: (state, action: PayloadAction<{
      sessionId: string;
      fileName: string;
      totalChunks: number;
    }>) => {
      const { sessionId, fileName, totalChunks } = action.payload;
      state.uploads[sessionId] = {
        sessionId,
        fileName,
        totalChunks,
        uploadedChunks: 0,
        progress: 0,
        status: 'uploading',
      };
    },
    updateChunkProgress: (state, action: PayloadAction<{
      sessionId: string;
      chunkIndex: number;
    }>) => {
      const { sessionId } = action.payload;
      const upload = state.uploads[sessionId];
      if (upload) {
        upload.uploadedChunks++;
        upload.progress = Math.round((upload.uploadedChunks / upload.totalChunks) * 100);
      }
    },
    completeUpload: (state, action: PayloadAction<{ sessionId: string }>) => {
      const { sessionId } = action.payload;
      const upload = state.uploads[sessionId];
      if (upload) {
        upload.status = 'completed';
        upload.progress = 100;
      }
    },
    failUpload: (state, action: PayloadAction<{
      sessionId: string;
      error: string;
    }>) => {
      const { sessionId, error } = action.payload;
      const upload = state.uploads[sessionId];
      if (upload) {
        upload.status = 'failed';
        upload.error = error;
      }
    },
    removeUpload: (state, action: PayloadAction<{ sessionId: string }>) => {
      const { sessionId } = action.payload;
      delete state.uploads[sessionId];
    },
    clearCompletedUploads: (state) => {
      Object.keys(state.uploads).forEach((sessionId) => {
        if (state.uploads[sessionId].status === 'completed') {
          delete state.uploads[sessionId];
        }
      });
    },
  },
});

export const {
  initializeUpload,
  updateChunkProgress,
  completeUpload,
  failUpload,
  removeUpload,
  clearCompletedUploads,
} = uploadSlice.actions;

export default uploadSlice.reducer;

import { useCallback } from 'react';
import { useDropzone } from 'react-dropzone';
import { useNavigate } from 'react-router-dom';
import { useAppDispatch, useAppSelector } from '../../store/hooks';
import { uploadFileChunked } from './uploadSlice';
import { useUploadDocumentMutation } from '../documents/documentsApi';
import { UploadProgress } from './UploadProgress';
import './DocumentUpload.css';

const USER_ID = 'user-001';
const MAX_SMALL_FILE_SIZE = 10 * 1024 * 1024; // 10MB

export const DocumentUpload = () => {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const uploads = useAppSelector((state) => state.upload.uploads);
  const [uploadSmallFile, { isLoading }] = useUploadDocumentMutation();

  const onDrop = useCallback(async (acceptedFiles: File[]) => {
    for (const file of acceptedFiles) {
      if (file.size > MAX_SMALL_FILE_SIZE) {
        // Use chunked upload for large files
        dispatch(uploadFileChunked({ file, userId: USER_ID }));
      } else {
        // Use simple upload for small files
        try {
          await uploadSmallFile({ userId: USER_ID, file }).unwrap();
          alert(`File "${file.name}" uploaded successfully!`);
        } catch (error) {
          alert(`Failed to upload "${file.name}"`);
        }
      }
    }
  }, [dispatch, uploadSmallFile]);

  const { getRootProps, getInputProps, isDragActive } = useDropzone({
    onDrop,
    multiple: true,
  });

  const uploadList = Object.values(uploads);

  return (
    <div className="document-upload">
      <h2>Upload Documents</h2>

      <div
        {...getRootProps()}
        className={`dropzone ${isDragActive ? 'dropzone-active' : ''}`}
      >
        <input {...getInputProps()} />
        <div className="dropzone-content">
          <div className="upload-icon">📁</div>
          {isDragActive ? (
            <p>Drop files here...</p>
          ) : (
            <>
              <p>Drag & drop files here, or click to select</p>
              <p className="text-muted text-small">
                Files larger than 10MB will use chunked upload
              </p>
            </>
          )}
        </div>
      </div>

      {isLoading && (
        <div className="loading">Uploading small file...</div>
      )}

      {uploadList.length > 0 && (
        <div className="uploads-section">
          <h3>Upload Progress</h3>
          <div className="uploads-list">
            {uploadList.map((upload) => (
              <UploadProgress key={upload.sessionId} upload={upload} />
            ))}
          </div>
        </div>
      )}

      <div className="upload-actions">
        <button onClick={() => navigate('/')} className="btn btn-secondary">
          Back to Documents
        </button>
      </div>
    </div>
  );
};

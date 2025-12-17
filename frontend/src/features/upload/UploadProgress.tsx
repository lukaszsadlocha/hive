import { useAppDispatch } from '../../store/hooks';
import { removeUpload } from './uploadSlice';
import type { ChunkUploadProgress } from '../../types';
import './UploadProgress.css';

interface UploadProgressProps {
  upload: ChunkUploadProgress;
}

export const UploadProgress = ({ upload }: UploadProgressProps) => {
  const dispatch = useAppDispatch();

  const handleRemove = () => {
    dispatch(removeUpload({ sessionId: upload.sessionId }));
  };

  return (
    <div className="upload-progress-card">
      <div className="upload-info">
        <div className="upload-name">{upload.fileName}</div>
        <div className="upload-stats">
          {upload.uploadedChunks} / {upload.totalChunks} chunks
        </div>
      </div>

      <div className="progress-bar-container">
        <div className="progress-bar" style={{ width: `${upload.progress}%` }}>
          <span className="progress-text">{upload.progress}%</span>
        </div>
      </div>

      <div className="upload-status">
        {upload.status === 'uploading' && <span className="status uploading">Uploading...</span>}
        {upload.status === 'completed' && <span className="status completed">✓ Completed</span>}
        {upload.status === 'failed' && <span className="status failed">✗ Failed</span>}
        {upload.error && <span className="error-message">{upload.error}</span>}
      </div>

      {(upload.status === 'completed' || upload.status === 'failed') && (
        <button onClick={handleRemove} className="btn btn-sm btn-secondary remove-btn">
          Remove
        </button>
      )}
    </div>
  );
};

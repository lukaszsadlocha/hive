import { useParams, useNavigate } from 'react-router-dom';
import { useState } from 'react';
import { useGetDocumentQuery, useDeleteDocumentMutation } from './documentsApi';
import { DocumentPreview } from './DocumentPreview';
import { ShareDialog } from '../share/ShareDialog';
import './DocumentDetails.css';

const USER_ID = 'user-001';

export const DocumentDetails = () => {
  const { id } = useParams<{ id: string }>();
  const navigate = useNavigate();
  const [showShareDialog, setShowShareDialog] = useState(false);

  const { data: document, isLoading, error } = useGetDocumentQuery(
    { id: id!, userId: USER_ID },
    { skip: !id }
  );

  const [deleteDocument] = useDeleteDocumentMutation();

  const handleDelete = async () => {
    if (confirm('Delete this document?')) {
      try {
        await deleteDocument({ id: id!, userId: USER_ID }).unwrap();
        navigate('/');
      } catch {
        alert('Failed to delete');
      }
    }
  };

  if (isLoading) return <div className="loading">Loading...</div>;
  if (error || !document) return <div className="error">Document not found</div>;

  return (
    <div className="document-details">
      <div className="details-header">
        <h2>{document.fileName}</h2>
        <div className="actions">
          <button onClick={() => setShowShareDialog(true)} className="btn btn-primary">
            Share
          </button>
          <button onClick={handleDelete} className="btn btn-danger">
            Delete
          </button>
        </div>
      </div>

      <div className="details-content">
        <div className="info-section">
          <div className="info-item">
            <strong>Size:</strong> {formatFileSize(document.fileSize)}
          </div>
          <div className="info-item">
            <strong>Type:</strong> {document.contentType}
          </div>
          <div className="info-item">
            <strong>Uploaded:</strong> {new Date(document.uploadedAt).toLocaleString()}
          </div>
          <div className="info-item">
            <strong>Status:</strong> <span className={`status-badge status-${document.status}`}>{document.status}</span>
          </div>
          <div className="info-item">
            <strong>Category:</strong> {document.metadata.category || 'Uncategorized'}
          </div>
          {document.metadata.tags.length > 0 && (
            <div className="info-item">
              <strong>Tags:</strong> {document.metadata.tags.map(tag => <span key={tag} className="badge">{tag}</span>)}
            </div>
          )}
        </div>

        <DocumentPreview documentId={id!} userId={USER_ID} />
      </div>

      {showShareDialog && (
        <ShareDialog
          documentId={id!}
          userId={USER_ID}
          onClose={() => setShowShareDialog(false)}
        />
      )}
    </div>
  );
};

function formatFileSize(bytes: number): string {
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
}

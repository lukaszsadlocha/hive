import { useGetDocumentPreviewQuery } from './documentsApi';

interface DocumentPreviewProps {
  documentId: string;
  userId: string;
}

export const DocumentPreview = ({ documentId, userId }: DocumentPreviewProps) => {
  const { data, isLoading, error } = useGetDocumentPreviewQuery({ id: documentId, userId });

  if (isLoading) return <div className="loading">Loading preview...</div>;
  if (error) return <div className="error">Failed to load preview</div>;

  return (
    <div className="document-preview">
      <h3>Preview</h3>
      <div className="preview-container">
        <iframe
          src={data?.previewUrl}
          style={{ width: '100%', height: '600px', border: '1px solid #dee2e6', borderRadius: '4px' }}
          title="Document Preview"
        />
      </div>
    </div>
  );
};

import { useState } from 'react';
import { useCreateShareLinkMutation } from '../documents/documentsApi';
import './ShareDialog.css';

interface ShareDialogProps {
  documentId: string;
  userId: string;
  onClose: () => void;
}

export const ShareDialog = ({ documentId, userId, onClose }: ShareDialogProps) => {
  const [expiresInHours, setExpiresInHours] = useState('24');
  const [password, setPassword] = useState('');
  const [shareLink, setShareLink] = useState('');

  const [createShare, { isLoading }] = useCreateShareLinkMutation();

  const handleCreate = async () => {
    try {
      const result = await createShare({
        userId,
        documentId,
        request: {
          expiresInHours: parseInt(expiresInHours),
          password: password || undefined,
        },
      }).unwrap();

      const fullUrl = `${window.location.origin}/share/${result.token}`;
      setShareLink(fullUrl);
    } catch {
      alert('Failed to create share link');
    }
  };

  return (
    <div className="dialog-overlay" onClick={onClose}>
      <div className="dialog" onClick={(e) => e.stopPropagation()}>
        <h3>Share Document</h3>

        {!shareLink ? (
          <>
            <div className="form-group">
              <label className="form-label">Expires in (hours)</label>
              <input
                type="number"
                className="form-input"
                value={expiresInHours}
                onChange={(e) => setExpiresInHours(e.target.value)}
                min="1"
                max="168"
              />
            </div>

            <div className="form-group">
              <label className="form-label">Password (optional)</label>
              <input
                type="password"
                className="form-input"
                value={password}
                onChange={(e) => setPassword(e.target.value)}
                placeholder="Leave empty for no password"
              />
            </div>

            <div className="dialog-actions">
              <button onClick={handleCreate} disabled={isLoading} className="btn btn-primary">
                {isLoading ? 'Creating...' : 'Create Link'}
              </button>
              <button onClick={onClose} className="btn btn-secondary">
                Cancel
              </button>
            </div>
          </>
        ) : (
          <>
            <div className="success">Link created successfully!</div>
            <div className="form-group">
              <label className="form-label">Share this link:</label>
              <input
                type="text"
                className="form-input"
                value={shareLink}
                readOnly
                onClick={(e) => e.currentTarget.select()}
              />
            </div>
            <button onClick={() => navigator.clipboard.writeText(shareLink)} className="btn btn-primary">
              Copy to Clipboard
            </button>
          </>
        )}
      </div>
    </div>
  );
};

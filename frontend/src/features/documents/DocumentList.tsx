import { Link } from 'react-router-dom';
import { useGetDocumentsQuery, useDeleteDocumentMutation } from './documentsApi';
import { useAppSelector, useAppDispatch } from '../../store/hooks';
import { setFilter, setSorting, clearFilters } from './documentsSlice';
import './DocumentList.css';

const USER_ID = 'user-001'; // Hardcoded for now

export const DocumentList = () => {
  const dispatch = useAppDispatch();
  const { filters, sorting } = useAppSelector((state) => state.documents);
  const pageSize = 20;

  const { data, isLoading, error } = useGetDocumentsQuery({
    userId: USER_ID,
    category: filters.category,
    sortBy: sorting.sortBy,
    sortOrder: sorting.sortOrder === 'asc' ? 'ASC' : 'DESC',
    pageSize,
  });

  const [deleteDocument] = useDeleteDocumentMutation();

  const handleDelete = async (id: string) => {
    if (confirm('Are you sure you want to delete this document?')) {
      try {
        await deleteDocument({ id, userId: USER_ID }).unwrap();
      } catch (err) {
        alert('Failed to delete document');
      }
    }
  };

  const handleFilterChange = (key: 'category' | 'status', value: string) => {
    dispatch(setFilter({ key, value: value || undefined }));
  };

  const handleSortChange = (sortBy: 'uploadedAt' | 'fileName' | 'fileSize') => {
    const sortOrder = sorting.sortBy === sortBy && sorting.sortOrder === 'desc' ? 'asc' : 'desc';
    dispatch(setSorting({ sortBy, sortOrder }));
  };

  if (isLoading) {
    return <div className="loading">Loading documents...</div>;
  }

  if (error) {
    return <div className="error">Error loading documents. Please try again.</div>;
  }

  const documents = data?.documents || [];

  return (
    <div className="document-list">
      <div className="list-header">
        <h2>Documents</h2>
        <Link to="/upload" className="btn btn-primary">
          Upload New Document
        </Link>
      </div>

      <div className="filters">
        <div className="form-group">
          <label className="form-label">Category</label>
          <select
            className="form-select"
            value={filters.category || ''}
            onChange={(e) => handleFilterChange('category', e.target.value)}
          >
            <option value="">All Categories</option>
            <option value="Finanse">Finanse</option>
            <option value="Prawne">Prawne</option>
            <option value="HR">HR</option>
            <option value="IT">IT</option>
            <option value="Marketing">Marketing</option>
          </select>
        </div>

        <div className="form-group">
          <label className="form-label">Status</label>
          <select
            className="form-select"
            value={filters.status || ''}
            onChange={(e) => handleFilterChange('status', e.target.value)}
          >
            <option value="">All Status</option>
            <option value="uploaded">Uploaded</option>
            <option value="processing">Processing</option>
            <option value="processed">Processed</option>
            <option value="failed">Failed</option>
          </select>
        </div>

        <button className="btn btn-secondary" onClick={() => dispatch(clearFilters())}>
          Clear Filters
        </button>
      </div>

      {documents.length === 0 ? (
        <div className="empty-state">
          <h3>No documents found</h3>
          <p>Upload your first document to get started</p>
          <Link to="/upload" className="btn btn-primary mt-2">
            Upload Document
          </Link>
        </div>
      ) : (
        <div className="table-container">
          <table className="documents-table">
            <thead>
              <tr>
                <th onClick={() => handleSortChange('fileName')} className="sortable">
                  Name {sorting.sortBy === 'fileName' && (sorting.sortOrder === 'asc' ? '↑' : '↓')}
                </th>
                <th onClick={() => handleSortChange('fileSize')} className="sortable">
                  Size {sorting.sortBy === 'fileSize' && (sorting.sortOrder === 'asc' ? '↑' : '↓')}
                </th>
                <th>Category</th>
                <th>Status</th>
                <th onClick={() => handleSortChange('uploadedAt')} className="sortable">
                  Uploaded {sorting.sortBy === 'uploadedAt' && (sorting.sortOrder === 'asc' ? '↑' : '↓')}
                </th>
                <th>Actions</th>
              </tr>
            </thead>
            <tbody>
              {documents.map((doc) => (
                <tr key={doc.id}>
                  <td>
                    <Link to={`/documents/${doc.id}`} className="document-link">
                      {doc.fileName}
                    </Link>
                  </td>
                  <td>{formatFileSize(doc.fileSize)}</td>
                  <td>
                    <span className="badge">{doc.metadata.category || 'Uncategorized'}</span>
                  </td>
                  <td>
                    <span className={`status-badge status-${doc.status}`}>
                      {doc.status}
                    </span>
                  </td>
                  <td>{new Date(doc.uploadedAt).toLocaleDateString()}</td>
                  <td>
                    <div className="actions">
                      <Link to={`/documents/${doc.id}`} className="btn btn-sm btn-secondary">
                        View
                      </Link>
                      <button
                        onClick={() => handleDelete(doc.id)}
                        className="btn btn-sm btn-danger"
                      >
                        Delete
                      </button>
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
};

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 Bytes';
  const k = 1024;
  const sizes = ['Bytes', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(k));
  return Math.round(bytes / Math.pow(k, i) * 100) / 100 + ' ' + sizes[i];
}

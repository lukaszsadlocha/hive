import { useState } from 'react';
import { Link } from 'react-router-dom';
import { useSearchDocumentsQuery } from '../documents/documentsApi';
import './SearchPage.css';

const USER_ID = 'user-001';

export const SearchPage = () => {
  const [searchText, setSearchText] = useState('');
  const [activeSearch, setActiveSearch] = useState('');

  const { data: results = [], isLoading } = useSearchDocumentsQuery(
    { userId: USER_ID, searchText: activeSearch },
    { skip: !activeSearch }
  );

  const handleSearch = (e: React.FormEvent) => {
    e.preventDefault();
    setActiveSearch(searchText);
  };

  return (
    <div className="search-page">
      <h2>Search Documents</h2>

      <form onSubmit={handleSearch} className="search-form">
        <input
          type="text"
          className="form-input search-input"
          placeholder="Search by filename, title, tags..."
          value={searchText}
          onChange={(e) => setSearchText(e.target.value)}
        />
        <button type="submit" className="btn btn-primary">
          Search
        </button>
      </form>

      {isLoading && <div className="loading">Searching...</div>}

      {activeSearch && !isLoading && (
        <div className="search-results">
          <h3>Found {results.length} results</h3>

          {results.length === 0 ? (
            <div className="empty-state">
              <p>No documents match your search</p>
            </div>
          ) : (
            <div className="results-list">
              {results.map((doc) => (
                <div key={doc.id} className="result-card">
                  <Link to={`/documents/${doc.id}`} className="result-title">
                    {doc.fileName}
                  </Link>
                  <div className="result-meta">
                    <span>{doc.metadata.category || 'Uncategorized'}</span>
                    <span>{new Date(doc.uploadedAt).toLocaleDateString()}</span>
                  </div>
                  {doc.metadata.description && (
                    <p className="result-description">{doc.metadata.description}</p>
                  )}
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  );
};

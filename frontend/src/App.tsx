import { BrowserRouter, Routes, Route } from 'react-router-dom';
import { Provider } from 'react-redux';
import { store } from './store/store';
import { Layout } from './components/Layout/Layout';
import { DocumentList } from './features/documents/DocumentList';
import { DocumentDetails } from './features/documents/DocumentDetails';
import { DocumentUpload } from './features/upload/DocumentUpload';
import { SearchPage } from './features/search/SearchPage';
import { ErrorBoundary } from './components/ErrorBoundary/ErrorBoundary';
import './App.css';

function App() {
  return (
    <ErrorBoundary>
      <Provider store={store}>
        <BrowserRouter>
          <Routes>
            <Route path="/" element={<Layout />}>
              <Route index element={<DocumentList />} />
              <Route path="upload" element={<DocumentUpload />} />
              <Route path="search" element={<SearchPage />} />
              <Route path="documents/:id" element={<DocumentDetails />} />
            </Route>
          </Routes>
        </BrowserRouter>
      </Provider>
    </ErrorBoundary>
  );
}

export default App;

import { Link, Outlet } from 'react-router-dom';
import './Layout.css';

export const Layout = () => {
  return (
    <div className="layout">
      <header className="header">
        <div className="header-content">
          <h1 className="logo">
            <Link to="/">📄 Document Manager</Link>
          </h1>
          <nav className="nav">
            <Link to="/" className="nav-link">Documents</Link>
            <Link to="/upload" className="nav-link">Upload</Link>
            <Link to="/search" className="nav-link">Search</Link>
          </nav>
        </div>
      </header>
      <main className="main-content">
        <Outlet />
      </main>
      <footer className="footer">
        <p>Document Management System with Azure CosmosDB & Functions</p>
      </footer>
    </div>
  );
};

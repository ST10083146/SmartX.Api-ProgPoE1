import React from 'react';
import { HashRouter, Routes, Route } from 'react-router-dom';
import Landing from './pages/Landing.jsx';
import IngestionDashboard from './pages/IngestionDashboard.jsx';

export default function App() {
  return (
    <HashRouter>
      <div className="app-shell">
        <header className="topbar">
          <h1>Smart-X IoT Mesh Gateway</h1>
          <span className="badge live">Part 1 — Ingestion &amp; Validation</span>
        </header>
        <Routes>
          <Route path="/" element={<Landing />} />
          <Route path="/ingestion" element={<IngestionDashboard />} />
        </Routes>
      </div>
    </HashRouter>
  );
}

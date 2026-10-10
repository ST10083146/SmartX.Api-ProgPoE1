import React from 'react';
import { HashRouter, Routes, Route } from 'react-router-dom';
import { TelemetryProvider } from './context/TelemetryContext.jsx';
import NavBar from './components/NavBar.jsx';
import AlertToast from './components/AlertToast.jsx';
import Landing from './pages/Landing.jsx';
import IngestionDashboard from './pages/IngestionDashboard.jsx';
import CommandStream from './pages/CommandStream.jsx';

export default function App() {
  return (
    // The provider sits ABOVE the router so the live data survives page changes.
    <TelemetryProvider>
      <HashRouter>
        <div className="app-shell">
          <header className="topbar">
            <h1>Smart-X IoT Mesh Gateway</h1>
            <span className="badge live">Part 2 — Real-Time Processing &amp; Device Management</span>
          </header>
          <NavBar />
          <Routes>
            <Route path="/" element={<Landing />} />
            <Route path="/ingestion" element={<IngestionDashboard />} />
            <Route path="/commands" element={<CommandStream />} />
          </Routes>
          <AlertToast />
        </div>
      </HashRouter>
    </TelemetryProvider>
  );
}

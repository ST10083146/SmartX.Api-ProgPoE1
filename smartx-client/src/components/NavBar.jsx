import React from 'react';
import { NavLink } from 'react-router-dom';
import { useTelemetry } from '../context/TelemetryContext.jsx';

export default function NavBar() {
  const { connected, faults } = useTelemetry();

  return (
    <nav className="navbar">
      <NavLink to="/" end className="nav-link">Home</NavLink>
      <NavLink to="/ingestion" className="nav-link">Sensor Ingestion</NavLink>
      <NavLink to="/commands" className="nav-link">
        Command Stream
        {faults.length > 0 && <span className="nav-count">{faults.length}</span>}
      </NavLink>
      {/* Not built yet: this is for the final PoE, so it is shown but disabled */}
      <span className="nav-link nav-disabled" title="Implemented in the final PoE">Network Topology</span>
      <span className={`badge ${connected ? 'live' : ''}`} style={{ marginLeft: 'auto' }}>
        {connected ? 'Live' : 'Offline'}
      </span>
    </nav>
  );
}

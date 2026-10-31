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
      <NavLink to="/topology" className="nav-link">Network Topology</NavLink>
      <span className={`badge ${connected ? 'live' : ''}`} style={{ marginLeft: 'auto' }}>
        {connected ? 'Live' : 'Offline'}
      </span>
    </nav>
  );
}

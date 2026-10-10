import React from 'react';
import { useTelemetry } from '../context/TelemetryContext.jsx';

// The active faults come from the API's HashSet, so each problem only ever appears once.
export default function FaultList() {
  const { faults } = useTelemetry();

  return (
    <ul className="plain-list">
      {faults.length === 0 && <li className="hint-text">No active faults.</li>}
      {faults.map((f) => (
        <li key={`${f.mac}-${f.type}`}>
          <span className={`status-pill ${f.type === 'Disconnected' ? 'disconnected' : 'alert'}`}>{f.type}</span>
          {f.nodeId}
        </li>
      ))}
    </ul>
  );
}

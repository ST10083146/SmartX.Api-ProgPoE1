import React from 'react';
import { useTelemetry } from '../context/TelemetryContext.jsx';

// Shows what the two queue lanes on the API are doing, so you can SEE the priority lane being used.
export default function QueueStats() {
  const { queueStats } = useTelemetry();
  const s = queueStats || { normalDepth: 0, priorityDepth: 0, totalEnqueued: 0, totalProcessed: 0, priorityProcessed: 0 };

  return (
    <div className="stat-row">
      <div className="stat"><span className="stat-label">Normal lane waiting</span><strong>{s.normalDepth}</strong></div>
      <div className="stat priority"><span className="stat-label">Priority lane waiting</span><strong>{s.priorityDepth}</strong></div>
      <div className="stat"><span className="stat-label">Total processed</span><strong>{s.totalProcessed}</strong></div>
      <div className="stat priority"><span className="stat-label">Priority processed</span><strong>{s.priorityProcessed}</strong></div>
    </div>
  );
}

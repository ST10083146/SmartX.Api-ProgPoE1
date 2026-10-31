import React, { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client.js';
import { useTelemetry } from '../context/TelemetryContext.jsx';

// Uses BFS or DFS on the API to list which devices are still connected to the gateway.
export default function ConnectedPanel({ zones }) {
  const { meshVersion } = useTelemetry();
  const [mode, setMode] = useState('bfs');
  const [zone, setZone] = useState('');
  const [result, setResult] = useState(null);

  const load = useCallback(() => {
    api.getConnected({ mode, zone }).then(setResult).catch(() => {});
  }, [mode, zone]);

  useEffect(() => {
    load();
    const timer = setInterval(load, 3000);
    return () => clearInterval(timer);
  }, [load, meshVersion]);

  return (
    <div>
      <div className="inline-form">
        <select value={mode} onChange={(e) => setMode(e.target.value)}>
          <option value="bfs">Breadth-First Search</option>
          <option value="dfs">Depth-First Search</option>
        </select>
        <select value={zone} onChange={(e) => setZone(e.target.value)}>
          <option value="">Whole network</option>
          {zones.map((z) => <option key={z} value={z}>{z} only</option>)}
        </select>
      </div>

      {result && (
        <>
          <div className="hint-text">{result.mode} from {result.start}: {result.order.length} node(s) connected</div>
          <ol className="plain-list numbered">
            {result.order.map((n) => (
              <li key={n.id}>{n.nodeId} <span className="lane-tag">{n.hops} hop{n.hops === 1 ? '' : 's'}</span></li>
            ))}
          </ol>
          {result.down.length > 0 && <p className="error-text">Down: {result.down.join(', ')}</p>}
          {result.isolated.length > 0 && <p className="error-text">Cut off from the gateway: {result.isolated.join(', ')}</p>}
        </>
      )}
    </div>
  );
}

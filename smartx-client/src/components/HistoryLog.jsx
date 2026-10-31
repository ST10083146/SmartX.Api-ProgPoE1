import React, { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client.js';

const PRESETS = [
  { key: '1m', label: 'Last minute', minutes: 1 },
  { key: '5m', label: 'Last 5 minutes', minutes: 5 },
  { key: '30m', label: 'Last 30 minutes', minutes: 30 },
  { key: '1h', label: 'Last hour', minutes: 60 },
  { key: 'all', label: 'Everything', minutes: 0 },
  { key: 'custom', label: 'Custom range…', minutes: 0 }
];

// Historical log of one sensor. The time range is answered by a range query on the AVL tree in the API.
export default function HistoryLog({ nodeId, label }) {
  const [preset, setPreset] = useState('5m');
  const [customFrom, setCustomFrom] = useState('');
  const [customTo, setCustomTo] = useState('');
  const [take, setTake] = useState(50);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);

  const load = useCallback(() => {
    if (!nodeId || nodeId === 'GATEWAY') return;

    let from = '';
    let to = '';
    if (preset === 'custom') {
      if (customFrom) from = new Date(customFrom).toISOString();
      if (customTo) to = new Date(customTo).toISOString();
    } else if (preset !== 'all') {
      const minutes = PRESETS.find((p) => p.key === preset).minutes;
      from = new Date(Date.now() - minutes * 60 * 1000).toISOString();
    }

    api.getNodeHistory(nodeId, { from, to, take })
      .then((data) => { setResult(data); setError(null); })
      .catch((err) => setError(err.message));
  }, [nodeId, preset, customFrom, customTo, take]);

  useEffect(() => {
    load();
    // presets are "last N minutes", so keep refreshing; a custom range stays fixed
    if (preset === 'custom') return undefined;
    const timer = setInterval(load, 3000);
    return () => clearInterval(timer);
  }, [load, preset]);

  if (!nodeId || nodeId === 'GATEWAY') {
    return <p className="hint-text">Select a sensor on the graph to see its historical telemetry log.</p>;
  }

  return (
    <div>
      <div className="history-controls">
        <select value={preset} onChange={(e) => setPreset(e.target.value)}>
          {PRESETS.map((p) => <option key={p.key} value={p.key}>{p.label}</option>)}
        </select>
        {preset === 'custom' && (
          <>
            <input type="datetime-local" value={customFrom} onChange={(e) => setCustomFrom(e.target.value)} />
            <input type="datetime-local" value={customTo} onChange={(e) => setCustomTo(e.target.value)} />
          </>
        )}
        <select value={take} onChange={(e) => setTake(Number(e.target.value))}>
          <option value={25}>25 rows</option>
          <option value={50}>50 rows</option>
          <option value={200}>200 rows</option>
        </select>
      </div>

      {error && <p className="error-text">{error}</p>}

      {result && (
        <>
          <div className="hint-text">
            <strong>{label}</strong>: AVL range query found {result.totalInRange} reading(s), touching {result.nodesVisited} of {result.treeSize} tree nodes
            (tree height {result.treeHeight}) in {result.elapsedMicroseconds} µs.
          </div>

          <div className="table-wrap">
            <table className="log-table">
              <thead>
                <tr><th>Time</th><th>Value</th><th>Status</th></tr>
              </thead>
              <tbody>
                {result.readings.map((r) => (
                  <tr key={r.t} className={r.anomalous ? 'bad-row' : ''}>
                    <td>{new Date(r.t).toLocaleTimeString()}</td>
                    <td>{Number(r.value).toFixed(1)}{result.unit}</td>
                    <td>{r.anomalous ? 'Out of range' : 'Normal'}</td>
                  </tr>
                ))}
                {result.readings.length === 0 && (
                  <tr><td colSpan="3" className="hint-text">No readings in that range.</td></tr>
                )}
              </tbody>
            </table>
          </div>
        </>
      )}
    </div>
  );
}

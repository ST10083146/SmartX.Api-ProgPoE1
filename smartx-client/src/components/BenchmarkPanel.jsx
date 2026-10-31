import React, { useState } from 'react';
import { api } from '../api/client.js';

// Builds a big fake history on the API and compares the AVL tree with a plain list scan.
export default function BenchmarkPanel() {
  const [size, setSize] = useState(200000);
  const [result, setResult] = useState(null);
  const [busy, setBusy] = useState(false);

  const run = async () => {
    setBusy(true);
    try {
      setResult(await api.runBenchmark(size));
    } finally {
      setBusy(false);
    }
  };

  return (
    <div>
      <div className="inline-form">
        <select value={size} onChange={(e) => setSize(Number(e.target.value))}>
          <option value={50000}>50,000 readings</option>
          <option value={200000}>200,000 readings</option>
          <option value={500000}>500,000 readings</option>
        </select>
        <button className="btn-sm" onClick={run} disabled={busy}>{busy ? 'Running…' : 'Run benchmark'}</button>
      </div>

      {result && (
        <table className="log-table">
          <tbody>
            <tr><td>History size</td><td>{result.readings.toLocaleString()} readings</td></tr>
            <tr><td>Readings in the 2-hour window</td><td>{result.resultsFound.toLocaleString()}</td></tr>
            <tr><td>AVL tree range query</td><td><strong>{result.avlMicroseconds} µs</strong> ({result.avlNodesVisited.toLocaleString()} nodes visited)</td></tr>
            <tr><td>Plain list scan</td><td>{result.linearMicroseconds} µs (every reading checked)</td></tr>
            <tr><td>Speed-up</td><td><strong>{result.speedup}× faster</strong></td></tr>
            <tr><td>Tree height</td><td>{result.treeHeight} (a perfectly balanced tree this size is about {Math.ceil(Math.log2(result.readings))})</td></tr>
            <tr><td>Balanced and correct?</td><td>{result.treeValid ? 'Yes, every node checked' : 'NO, something is wrong'}</td></tr>
          </tbody>
        </table>
      )}
    </div>
  );
}

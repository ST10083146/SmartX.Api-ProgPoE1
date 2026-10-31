import React, { useState } from 'react';
import { api } from '../api/client.js';

// Add or remove a wireless link between two nodes (the edges of the graph).
export default function LinkEditor({ nodes, onChanged, onMessage }) {
  const [a, setA] = useState('');
  const [b, setB] = useState('');
  const [cost, setCost] = useState(10);

  const act = async (fn, okText) => {
    try {
      await fn();
      onMessage({ ok: true, text: okText });
      onChanged();
    } catch (err) {
      onMessage({ ok: false, text: err.message });
    }
  };

  return (
    <div>
      <div className="inline-form">
        <select value={a} onChange={(e) => setA(e.target.value)}>
          <option value="">Node A</option>
          {nodes.map((n) => <option key={n.id} value={n.id}>{n.nodeId}</option>)}
        </select>
        <select value={b} onChange={(e) => setB(e.target.value)}>
          <option value="">Node B</option>
          {nodes.map((n) => <option key={n.id} value={n.id}>{n.nodeId}</option>)}
        </select>
        <input type="number" min="1" value={cost} onChange={(e) => setCost(e.target.value)} style={{ width: '5rem' }} title="Power cost" />
      </div>
      <div className="button-row" style={{ marginTop: '0.5rem' }}>
        <button className="btn-sm" disabled={!a || !b} onClick={() => act(() => api.addLink(a, b, cost), 'Link added.')}>Add link</button>
        <button className="btn-sm secondary" disabled={!a || !b} onClick={() => act(() => api.removeLink(a, b), 'Link removed.')}>Remove link</button>
      </div>
    </div>
  );
}

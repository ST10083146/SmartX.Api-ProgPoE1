import React, { useState } from 'react';

// Lets the user type any MAC address or node id and shows the path its telemetry takes to the gateway.
export default function RoutePanel({ nodes, route, onTrace }) {
  const [text, setText] = useState('');

  const submit = (e) => {
    e.preventDefault();
    if (text.trim()) onTrace(text.trim());
  };

  const hopsText = (hops) => hops.map((h) => h.nodeId).join('  →  ');

  return (
    <div>
      <form className="inline-form" onSubmit={submit}>
        <input
          list="mesh-node-list"
          placeholder="MAC address or node id"
          value={text}
          onChange={(e) => setText(e.target.value)}
        />
        <datalist id="mesh-node-list">
          {nodes.filter((n) => !n.isGateway).map((n) => (
            <option key={n.id} value={n.id}>{n.nodeId}</option>
          ))}
        </datalist>
        <button type="submit" className="btn-sm">Trace route</button>
      </form>

      {!route && <p className="hint-text">Click a node on the graph, or type a MAC address / node id, to see its route.</p>}

      {route && !route.found && <p className="error-text">{route.message}</p>}

      {route && route.found && route.hops.length > 1 && (
        <div className="route-box">
          <div className="route-title">Power-efficient route (minimum spanning tree)</div>
          <div className="route-path">{hopsText(route.hops)}</div>
          <div className="hint-text">{route.hopCount} hop(s), total power cost {route.totalCost}</div>

          <div className="route-title" style={{ marginTop: '0.6rem' }}>Cheapest single path (Dijkstra)</div>
          <div className="route-path">{hopsText(route.shortestHops)}</div>
          <div className="hint-text">total power cost {route.shortestCost}</div>

          <p className="hint-text">
            The tree keeps the whole network cheap to run. Dijkstra only looks at this one node, so its path can
            cost less for this node but is not part of the cheapest overall backbone.
          </p>
        </div>
      )}

      {route && route.found && route.hops.length <= 1 && <p className="hint-text">{route.message}</p>}
    </div>
  );
}

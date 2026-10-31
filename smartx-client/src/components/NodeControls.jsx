import React from 'react';

// Fail / recover the selected node and show how the routes of the other nodes changed.
export default function NodeControls({ node, report, onFail, onRecover }) {
  if (!node || node.isGateway) {
    return <p className="hint-text">Select a device on the graph to simulate a failure and watch the network heal.</p>;
  }

  return (
    <div>
      <div className="hint-text">
        Selected: <strong>{node.nodeId}</strong> ({node.zone}) — {node.up ? 'online' : 'DOWN'}
      </div>

      <div className="button-row" style={{ marginTop: '0.5rem' }}>
        <button className="btn-sm danger" disabled={!node.up} onClick={() => onFail(node.id)}>Simulate failure</button>
        <button className="btn-sm" disabled={node.up} onClick={() => onRecover(node.id)}>Recover node</button>
      </div>

      {report && (
        <div className="route-box">
          <div className="route-title">{report.summary}</div>
          <div className="hint-text">Network power cost: {report.mstCostBefore} → {report.mstCostAfter}</div>
          <ul className="plain-list">
            {report.changes.map((c) => (
              <li key={c.id}>
                <strong>{c.nodeId}</strong>
                <div className="hint-text">{c.oldPath.length ? c.oldPath.join(' → ') : 'no route'}</div>
                <div>{c.isolated ? 'CUT OFF from the gateway' : `${c.newPath.join(' → ')} (next hop: ${c.nextHop})`}</div>
              </li>
            ))}
          </ul>
        </div>
      )}
    </div>
  );
}

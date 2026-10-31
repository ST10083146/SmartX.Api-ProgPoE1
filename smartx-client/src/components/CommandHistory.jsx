import React from 'react';

// The list comes from the API's Stack<T>, so the newest command is always first and Undo reverses it.
export default function CommandHistory({ commands, onUndo, onShutDownAll }) {
  return (
    <div>
      <div className="button-row">
        <button className="btn-sm" onClick={onUndo} disabled={commands.length === 0}>Undo last</button>
        <button className="btn-sm danger" onClick={onShutDownAll}>Shut down all</button>
      </div>
      <ul className="plain-list">
        {commands.length === 0 && <li className="hint-text">No override commands yet.</li>}
        {commands.map((c, i) => (
          <li key={c.id}>
            {i === 0 && <span className="lane-tag">next undo</span>}
            {c.description}
          </li>
        ))}
      </ul>
    </div>
  );
}

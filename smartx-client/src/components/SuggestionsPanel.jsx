import React from 'react';

export default function SuggestionsPanel({ data, patterns = [], onApply, onSeedDemo }) {
  const suggestions = data?.suggestions ?? [];
  const insights = data?.insights ?? [];

  return (
    <div>
      <h3 className="sub-heading">Suggested Actions</h3>
      {suggestions.length === 0 && (
        <p className="hint-text">
          No suggestion right now. They appear when a device that has an alert is one the engine has seen you react to
          at least twice. Press <strong>Load demo history</strong> below to see one straight away.
        </p>
      )}
      {suggestions.map((s, i) => (
        <div key={i} className="suggestion">
          <div><strong>{s.title}</strong> <span className="lane-tag">{Math.round(s.confidence * 100)}%</span></div>
          <div className="hint-text">{s.reason}</div>
          <button className="btn-sm" onClick={() => onApply(s)}>{s.kind === 'command' ? 'Apply' : 'Show'}</button>
        </div>
      ))}

      <h3 className="sub-heading">What the engine has learned</h3>
      {patterns.length === 0 && (
        <p className="hint-text">Nothing yet. Every time you react to an alert, the engine remembers what you did.</p>
      )}
      <ul className="plain-list">
        {patterns.map((p, i) => (
          <li key={i}>
            {p.trigger} → <strong>{p.action}</strong>
            <div className="hint-text">
              done {p.timesDone} time{p.timesDone === 1 ? '' : 's'}
              {p.ready ? ' · ready to suggest' : ' · needs to be seen twice'}
              {p.isDemo ? ' · includes demo history' : ''}
            </div>
          </li>
        ))}
      </ul>
      <button className="btn-sm secondary" onClick={onSeedDemo}>Load demo history</button>

      <h3 className="sub-heading">Automated Insights</h3>
      {insights.length === 0 && <p className="hint-text">Nothing unusual right now.</p>}
      {insights.map((n, i) => (
        <div key={i} className={`insight ${n.level}`}>{n.message}</div>
      ))}
    </div>
  );
}

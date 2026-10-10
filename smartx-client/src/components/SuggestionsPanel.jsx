import React from 'react';

export default function SuggestionsPanel({ data, onApply }) {
  const suggestions = data?.suggestions ?? [];
  const insights = data?.insights ?? [];

  return (
    <div>
      <h3 className="sub-heading">Suggested Actions</h3>
      {suggestions.length === 0 && (
        <p className="hint-text">No suggestions yet. They appear after the same alert-then-action pattern has happened twice.</p>
      )}
      {suggestions.map((s, i) => (
        <div key={i} className="suggestion">
          <div><strong>{s.title}</strong> <span className="lane-tag">{Math.round(s.confidence * 100)}%</span></div>
          <div className="hint-text">{s.reason}</div>
          <button className="btn-sm" onClick={() => onApply(s)}>{s.kind === 'command' ? 'Apply' : 'Show'}</button>
        </div>
      ))}

      <h3 className="sub-heading">Automated Insights</h3>
      {insights.length === 0 && <p className="hint-text">Nothing unusual right now.</p>}
      {insights.map((n, i) => (
        <div key={i} className={`insight ${n.level}`}>{n.message}</div>
      ))}
    </div>
  );
}

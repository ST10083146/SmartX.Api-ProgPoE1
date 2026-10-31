import React from 'react';

const CATEGORIES = ['', 'Environmental', 'PowerConsumption', 'Actuator'];
const STATES = [
  { value: '', label: 'Any status' },
  { value: 'online', label: 'Online' },
  { value: 'alert', label: 'In alert' },
  { value: 'disconnected', label: 'Disconnected' }
];

export default function FilterBar({ text, onTextChange, onSearch, category, onCategoryChange, state, onStateChange }) {
  const submit = (e) => {
    e.preventDefault();
    onSearch();
  };

  return (
    <form className="filter-bar" onSubmit={submit}>
      <input
        placeholder="Search by node, MAC, zone, room…"
        value={text}
        onChange={(e) => onTextChange(e.target.value)}
      />
      <select value={category} onChange={(e) => onCategoryChange(e.target.value)}>
        {CATEGORIES.map((c) => <option key={c} value={c}>{c === '' ? 'All categories' : c}</option>)}
      </select>
      <select value={state} onChange={(e) => onStateChange(e.target.value)}>
        {STATES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
      </select>
      <button type="submit" className="btn-sm">Search</button>
    </form>
  );
}

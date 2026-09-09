import React, { useState } from 'react';
import { api } from '../api/client.js';

const CATEGORIES = ['Environmental', 'PowerConsumption', 'Actuator'];

export default function SensorRegistrationForm({ onRegistered }) {
  const [form, setForm] = useState({
    macAddress: '',
    room: '',
    zone: '',
    nodeId: '',
    category: 'Environmental'
  });
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);

  const update = (field) => (e) => setForm({ ...form, [field]: e.target.value });

  const submit = async (e) => {
    e.preventDefault();
    setError(null);
    setBusy(true);
    try {
      const sensor = await api.registerSensor(form);
      onRegistered(sensor);
      setForm({ macAddress: '', room: '', zone: '', nodeId: '', category: 'Environmental' });
    } catch (err) {
      setError(err.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <form onSubmit={submit}>
      <label htmlFor="mac">Device MAC Address / Unique ID</label>
      <input id="mac" required value={form.macAddress} onChange={update('macAddress')} placeholder="AA:BB:CC:00:11:22" />

      <label htmlFor="zone">Zone</label>
      <input id="zone" required value={form.zone} onChange={update('zone')} placeholder="Zone 1" />

      <label htmlFor="room">Sub-Zone / Room</label>
      <input id="room" required value={form.room} onChange={update('room')} placeholder="Room 4" />

      <label htmlFor="nodeId">Node ID</label>
      <input id="nodeId" required value={form.nodeId} onChange={update('nodeId')} placeholder="node-esp32-07" />

      <label htmlFor="category">Sensor Category</label>
      <select id="category" value={form.category} onChange={update('category')}>
        {CATEGORIES.map((c) => (
          <option key={c} value={c}>{c}</option>
        ))}
      </select>

      {error && <p className="error-text">{error}</p>}
      <p className="hint-text">
        Registration is validated against the Facility → Zone → Sub-Zone → Node tree recursively before it's accepted.
      </p>

      <button type="submit" disabled={busy}>{busy ? 'Registering…' : 'Register Sensor'}</button>
    </form>
  );
}

import React, { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { api } from '../api/client.js';
import SensorRegistrationForm from '../components/SensorRegistrationForm.jsx';
import FileUploader from '../components/FileUploader.jsx';
import LiveTelemetryFeed from '../components/LiveTelemetryFeed.jsx';

export default function IngestionDashboard() {
  const [sensors, setSensors] = useState([]);
  const [selectedId, setSelectedId] = useState(null);

  const refresh = () => api.getSensors().then(setSensors).catch(() => {});

  useEffect(() => { refresh(); }, []);

  const onRegistered = (sensor) => {
    setSensors((prev) => [...prev, sensor]);
    setSelectedId(sensor.id);
  };

  const onUploaded = () => refresh();

  const selected = sensors.find((s) => s.id === selectedId) ?? null;

  return (
    <main>
      <div style={{ padding: '0 1.5rem' }}>
        <Link to="/" className="hint-text">&larr; Back to gateway menu</Link>
      </div>

      <div className="dashboard">
        <div style={{ display: 'flex', flexDirection: 'column', gap: '1.5rem' }}>
          <div className="panel">
            <h2>Register Sensor</h2>
            <SensorRegistrationForm onRegistered={onRegistered} />
          </div>

          <div className="panel">
            <h2>Registered Sensors ({sensors.length})</h2>
            <ul className="sensor-list">
              {sensors.map((s) => (
                <li
                  key={s.id}
                  onClick={() => setSelectedId(s.id)}
                  style={{ cursor: 'pointer', border: s.id === selectedId ? '1px solid var(--mauve)' : 'none' }}
                >
                  <span>{s.nodeId} · {s.category}</span>
                  <span>{s.zone}</span>
                </li>
              ))}
              {sensors.length === 0 && <p className="hint-text">No sensors registered yet.</p>}
            </ul>
          </div>

          <div className="panel">
            <h2>Attach Config / Photo / Log</h2>
            <FileUploader sensor={selected} onUploaded={onUploaded} />
          </div>
        </div>

        <div className="panel">
          <h2>Live Telemetry Feed</h2>
          <LiveTelemetryFeed />
        </div>
      </div>
    </main>
  );
}

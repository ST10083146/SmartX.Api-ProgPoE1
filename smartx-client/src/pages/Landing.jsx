import React, { useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { api } from '../api/client.js';

const ROUTES = {
  ingestion: '/ingestion'
};

export default function Landing() {
  const [pillars, setPillars] = useState([]);
  const [error, setError] = useState(null);
  const navigate = useNavigate();

  useEffect(() => {
    api.getPillars().then(setPillars).catch((e) => setError(e.message));
  }, []);

  return (
    <main className="pillars">
      {error && <p className="error-text">{error}</p>}
      {pillars.map((p) => (
        <div
          key={p.key}
          className={`pillar-card ${p.enabled ? 'enabled' : 'disabled'}`}
          onClick={() => p.enabled && navigate(ROUTES[p.key])}
        >
          <h2>{p.label}</h2>
          <p>{p.enabled ? 'Available in this part of the PoE.' : 'Disabled — implemented in a later part.'}</p>
        </div>
      ))}
    </main>
  );
}

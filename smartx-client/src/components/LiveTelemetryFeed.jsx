import React, { useEffect, useRef, useState } from 'react';
import { createTelemetryConnection } from '../api/client.js';

const MAX_FEED_ITEMS = 40;

/**
 * This is the "dynamic engagement feature" from the brief: instead of a
 * static progress bar, sensor readings stream in live over SignalR and
 * render as a scrolling feed, with out-of-range readings picked out in red
 * and surfaced again as a toast — so a developer watching the dashboard
 * can spot a spike or a dropped sensor the moment it happens, not after
 * refreshing a page.
 */
export default function LiveTelemetryFeed() {
  const [items, setItems] = useState([]);
  const [alert, setAlert] = useState(null);
  const [aggregate, setAggregate] = useState(null);
  const [connected, setConnected] = useState(false);
  const alertTimer = useRef(null);

  useEffect(() => {
    const connection = createTelemetryConnection();

    connection.on('telemetryTick', (tick) => {
      setItems((prev) => [tick, ...prev].slice(0, MAX_FEED_ITEMS));
    });

    connection.on('anomalyAlert', (payload) => {
      setAlert(payload);
      clearTimeout(alertTimer.current);
      alertTimer.current = setTimeout(() => setAlert(null), 4000);
    });

    connection.on('meterAggregate', (payload) => setAggregate(payload));

    connection.start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false));

    return () => {
      clearTimeout(alertTimer.current);
      connection.stop();
    };
  }, []);

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span className={`badge ${connected ? 'live' : ''}`}>{connected ? 'Live' : 'Connecting…'}</span>
        {aggregate && (
          <span className="hint-text">Meter aggregate ({aggregate.label}): {aggregate.watts.toFixed(1)}W</span>
        )}
      </div>

      <div className="feed" style={{ marginTop: '1rem' }}>
        {items.length === 0 && <p className="hint-text">Waiting for telemetry… register a sensor to start seeding data.</p>}
        {items.map((tick, i) => (
          <div key={`${tick.sensorId}-${tick.timestamp}-${i}`} className={`feed-item ${tick.anomalous ? 'anomalous' : ''}`}>
            <span>{tick.nodeId} · {tick.category}</span>
            <span>{Number(tick.value).toFixed(1)}{tick.unit}</span>
          </div>
        ))}
      </div>

      {alert && <div className="alert-toast">⚠ {alert.message}</div>}
    </div>
  );
}

import React, { useEffect, useState } from 'react';
import { api } from '../api/client.js';

// Draws the selected device's history as a line. The API returns it already sorted by time
// (it comes out of a SortedDictionary), so no sorting is needed here.
export default function TimelineChart({ device }) {
  const [points, setPoints] = useState([]);

  useEffect(() => {
    if (!device) return undefined;
    let cancelled = false;

    const load = () =>
      api.getDeviceHistory(device.mac, 60)
        .then((data) => { if (!cancelled) setPoints(data); })
        .catch(() => {});

    load();
    const timer = setInterval(load, 2000);
    return () => { cancelled = true; clearInterval(timer); };
  }, [device?.mac]);

  if (!device) return <p className="hint-text">Click a device card to see its timeline.</p>;
  if (points.length < 2) return <p className="hint-text">Collecting readings for {device.nodeId}…</p>;

  const width = 600;
  const height = 160;
  const pad = 12;
  const values = points.map((p) => p.value);
  const min = Math.min(...values);
  const max = Math.max(...values);
  const range = max - min || 1;

  const x = (i) => pad + (i / (points.length - 1)) * (width - pad * 2);
  const y = (v) => height - pad - ((v - min) / range) * (height - pad * 2);
  const line = points.map((p, i) => `${x(i)},${y(p.value)}`).join(' ');

  return (
    <div>
      <div className="hint-text">
        {device.nodeId} — last {points.length} readings (min {min.toFixed(1)}, max {max.toFixed(1)}{device.unit})
      </div>
      <svg viewBox={`0 0 ${width} ${height}`} className="timeline">
        <polyline points={line} fill="none" stroke="var(--sky)" strokeWidth="2" />
        {points.map((p, i) => (
          p.anomalous && <circle key={i} cx={x(i)} cy={y(p.value)} r="4" fill="var(--red)" />
        ))}
      </svg>
    </div>
  );
}

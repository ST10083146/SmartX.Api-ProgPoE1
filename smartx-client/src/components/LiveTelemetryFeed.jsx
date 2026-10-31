import React from 'react';
import { useTelemetry } from '../context/TelemetryContext.jsx';

/**
 * Part 1's dynamic engagement feature (live feed + anomaly toast).
 * In Part 2 it reads from the shared TelemetryContext instead of opening its own connection,
 * so the feed is still there when you come back from another page.
 */
export default function LiveTelemetryFeed() {
  const { feed, aggregate, connected } = useTelemetry();

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <span className={`badge ${connected ? 'live' : ''}`}>{connected ? 'Live' : 'Connecting…'}</span>
        {aggregate && (
          <span className="hint-text">Meter aggregate ({aggregate.label}): {aggregate.watts.toFixed(1)}W</span>
        )}
      </div>

      <div className="feed" style={{ marginTop: '1rem' }}>
        {feed.length === 0 && <p className="hint-text">Waiting for telemetry… register a sensor to start the simulated devices.</p>}
        {feed.map((tick, i) => (
          <div key={`${tick.sensorId}-${tick.timestamp}-${i}`} className={`feed-item ${tick.anomalous ? 'anomalous' : ''}`}>
            <span>
              {tick.nodeId} · {tick.category}
              {tick.lane === 'priority' && <span className="lane-tag">priority</span>}
            </span>
            <span>{Number(tick.value).toFixed(1)}{tick.unit}</span>
          </div>
        ))}
      </div>

    </div>
  );
}

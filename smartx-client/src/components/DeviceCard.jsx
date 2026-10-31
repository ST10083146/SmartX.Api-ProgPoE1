import React from 'react';

export default function DeviceCard({ device, selected, onSelect, onCommand, onSimulate }) {
  const isActuator = device.category === 'Actuator';
  const disconnected = device.status === 'Disconnected';

  const valueText = () => {
    if (device.lastValue === null || device.lastValue === undefined) return '—';
    if (isActuator) return device.lastValue >= 0.5 ? 'ON' : 'OFF';
    return `${Number(device.lastValue).toFixed(1)}${device.unit}`;
  };

  return (
    <div
      className={`device-card status-${device.status.toLowerCase()} ${selected ? 'selected' : ''}`}
      onClick={() => onSelect(device.mac)}
    >
      <div className="device-head">
        <strong>{device.nodeId}</strong>
        <span className={`status-pill ${device.status.toLowerCase()}`}>{device.status}</span>
      </div>
      <div className="device-sub">{device.category} · {device.zone} / {device.room}</div>
      <div className="device-value">{valueText()}</div>

      {isActuator && (
        <div className="hint-text">Commanded: <strong>{device.commandedOn ? 'ON' : 'OFF'}</strong></div>
      )}

      <div className="device-actions" onClick={(e) => e.stopPropagation()}>
        {isActuator && (
          <>
            <button className="btn-sm" disabled={device.commandedOn} onClick={() => onCommand(device.mac, 'On')}>Turn ON</button>
            <button className="btn-sm secondary" disabled={!device.commandedOn} onClick={() => onCommand(device.mac, 'Off')}>Turn OFF</button>
          </>
        )}
        {!isActuator && (
          <>
            <button className="btn-sm danger" onClick={() => onSimulate('spike', device.mac)}>Simulate spike</button>
            <button className="btn-sm secondary" onClick={() => onSimulate('drift', device.mac)}>Simulate drift</button>
          </>
        )}
        {disconnected
          ? <button className="btn-sm secondary" onClick={() => onSimulate('reconnect', device.mac)}>Reconnect</button>
          : <button className="btn-sm secondary" onClick={() => onSimulate('disconnect', device.mac)}>Unplug</button>}
      </div>
    </div>
  );
}

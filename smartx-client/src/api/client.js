import * as signalR from '@microsoft/signalr';

export const API_BASE = 'http://localhost:5080';

async function handle(res) {
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    throw new Error(body.errors?.join(', ') || `Request failed (${res.status})`);
  }
  return res.json();
}

export const api = {
  getPillars: () => fetch(`${API_BASE}/api/pillars`).then(handle),
  getSensors: () => fetch(`${API_BASE}/api/sensors`).then(handle),
  registerSensor: (payload) =>
    fetch(`${API_BASE}/api/sensors`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(payload)
    }).then(handle),
  getHistory: (sensorId) => fetch(`${API_BASE}/api/sensors/${sensorId}/history`).then(handle),
  uploadFile: (sensorId, file) => {
    const form = new FormData();
    form.append('file', file);
    return fetch(`${API_BASE}/api/sensors/${sensorId}/upload`, {
      method: 'POST',
      body: form
    }).then(handle);
  }
};

// Single shared hub connection for the live engagement feed — components
// subscribe/unsubscribe to events rather than each opening their own socket.
export function createTelemetryConnection() {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${API_BASE}/hubs/telemetry`)
    .withAutomaticReconnect()
    .build();
}

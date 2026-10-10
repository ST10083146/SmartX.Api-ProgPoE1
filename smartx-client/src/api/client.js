import * as signalR from '@microsoft/signalr';

export const API_BASE = 'http://localhost:5080';

async function handle(res) {
  if (!res.ok) {
    const body = await res.json().catch(() => ({}));
    const message = body.errors?.join(', ') || body.error || `Request failed (${res.status})`;
    throw new Error(message);
  }
  return res.json();
}

const json = (method, url, body) =>
  fetch(`${API_BASE}${url}`, {
    method,
    headers: { 'Content-Type': 'application/json' },
    body: body ? JSON.stringify(body) : undefined
  }).then(handle);

export const api = {
  // ---- Part 1 ----
  getPillars: () => fetch(`${API_BASE}/api/pillars`).then(handle),
  getSensors: () => fetch(`${API_BASE}/api/sensors`).then(handle),
  registerSensor: (payload) => json('POST', '/api/sensors', payload),
  getHistory: (sensorId) => fetch(`${API_BASE}/api/sensors/${sensorId}/history`).then(handle),
  uploadFile: (sensorId, file) => {
    const form = new FormData();
    form.append('file', file);
    return fetch(`${API_BASE}/api/sensors/${sensorId}/upload`, { method: 'POST', body: form }).then(handle);
  },

  // ---- Part 2 ----
  // log=true is only sent when the user presses Search, so typing doesn't spam the pattern engine
  getDevices: ({ q = '', category = '', state = '', log = false } = {}) => {
    const params = new URLSearchParams();
    if (q) params.set('q', q);
    if (category) params.set('category', category);
    if (state) params.set('state', state);
    if (log) params.set('log', 'true');
    return fetch(`${API_BASE}/api/devices?${params.toString()}`).then(handle);
  },
  getDeviceHistory: (mac, take = 60) =>
    fetch(`${API_BASE}/api/devices/${encodeURIComponent(mac)}/history?take=${take}`).then(handle),
  getFaults: () => fetch(`${API_BASE}/api/faults`).then(handle),
  getQueueStats: () => fetch(`${API_BASE}/api/queues/stats`).then(handle),
  getSuggestions: () => fetch(`${API_BASE}/api/suggestions`).then(handle),
  getCommandHistory: () => fetch(`${API_BASE}/api/commands/history`).then(handle),

  sendCommand: (macAddress, action) => json('POST', '/api/commands', { macAddress, action }),
  shutDownAll: () => json('POST', '/api/commands/shutdown-all'),
  undoCommand: () => json('POST', '/api/commands/undo'),

  // Simulator buttons: these make a fake device misbehave, it still reports through /api/telemetry
  simulateSpike: (mac) => json('POST', `/api/simulator/spike/${encodeURIComponent(mac)}`),
  simulateDrift: (mac) => json('POST', `/api/simulator/drift/${encodeURIComponent(mac)}`),
  simulateDisconnect: (mac) => json('POST', `/api/simulator/disconnect/${encodeURIComponent(mac)}`),
  simulateReconnect: (mac) => json('POST', `/api/simulator/reconnect/${encodeURIComponent(mac)}`)
};

// One SignalR connection is created in TelemetryContext and shared by every page.
export function createTelemetryConnection() {
  return new signalR.HubConnectionBuilder()
    .withUrl(`${API_BASE}/hubs/telemetry`)
    .withAutomaticReconnect()
    .build();
}

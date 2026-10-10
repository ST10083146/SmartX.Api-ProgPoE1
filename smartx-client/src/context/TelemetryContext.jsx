import React, { createContext, useContext, useEffect, useState } from 'react';
import { api, createTelemetryConnection } from '../api/client.js';

/**
 * Part 2 requires navigating between pages "without losing application state".
 * In Part 1 each page opened its own SignalR connection, so the live feed was wiped
 * every time you changed page. Now ONE connection and ONE set of state lives up here in
 * the provider (which sits above the router), so pages can come and go without losing anything.
 *
 * Reference (full list in README section 10, IIE Harvard Anglia style):
 *   React (n.d.) Passing Data Deeply with Context.
 *     Available at: https://react.dev/learn/passing-data-deeply-with-context (Accessed: 10 October 2026).
 *   Jovanović, M. (n.d.) Adding Real-Time Functionality To .NET Applications With SignalR.
 *     Available at: https://milanjovanovic.tech/blog/adding-real-time-functionality-to-dotnet-applications-with-signalr (Accessed: 10 October 2026).
 */
const TelemetryContext = createContext(null);

const MAX_FEED_ITEMS = 40;

export function TelemetryProvider({ children }) {
  const [devices, setDevices] = useState({});      // mac -> device info
  const [feed, setFeed] = useState([]);
  const [toast, setToast] = useState(null);
  const [aggregate, setAggregate] = useState(null);
  const [queueStats, setQueueStats] = useState(null);
  const [faults, setFaults] = useState([]);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    const connection = createTelemetryConnection();
    let toastTimer = null;

    const upsertDevice = (info) => setDevices((prev) => ({ ...prev, [info.mac]: info }));
    const refreshFaults = () => api.getFaults().then(setFaults).catch(() => {});

    connection.on('deviceUpdate', upsertDevice);

    connection.on('telemetryTick', (tick) => {
      setFeed((prev) => [tick, ...prev].slice(0, MAX_FEED_ITEMS));
    });

    connection.on('anomalyAlert', (payload) => {
      setToast(payload);
      clearTimeout(toastTimer);
      toastTimer = setTimeout(() => setToast(null), 4000);
    });

    connection.on('meterAggregate', setAggregate);
    connection.on('queueStats', setQueueStats);
    connection.on('faultRaised', refreshFaults);
    connection.on('faultCleared', refreshFaults);

    connection.start()
      .then(() => setConnected(true))
      .catch(() => setConnected(false));
    connection.onclose(() => setConnected(false));
    connection.onreconnected(() => setConnected(true));

    // initial load (in case devices already exist before the dashboard opens)
    api.getDevices().then((list) => {
      const map = {};
      list.forEach((d) => { map[d.mac] = d; });
      setDevices(map);
    }).catch(() => {});
    refreshFaults();
    api.getQueueStats().then(setQueueStats).catch(() => {});

    return () => {
      clearTimeout(toastTimer);
      connection.stop();
    };
  }, []);

  const value = { devices, feed, toast, aggregate, queueStats, faults, connected };
  return <TelemetryContext.Provider value={value}>{children}</TelemetryContext.Provider>;
}

export function useTelemetry() {
  return useContext(TelemetryContext);
}

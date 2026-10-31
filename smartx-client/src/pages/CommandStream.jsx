import React, { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client.js';
import { useTelemetry } from '../context/TelemetryContext.jsx';
import QueueStats from '../components/QueueStats.jsx';
import FilterBar from '../components/FilterBar.jsx';
import DeviceCard from '../components/DeviceCard.jsx';
import TimelineChart from '../components/TimelineChart.jsx';
import CommandHistory from '../components/CommandHistory.jsx';
import FaultList from '../components/FaultList.jsx';
import SuggestionsPanel from '../components/SuggestionsPanel.jsx';

export default function CommandStream() {
  const { devices } = useTelemetry();

  // filters
  const [text, setText] = useState('');
  const [appliedText, setAppliedText] = useState('');
  const [category, setCategory] = useState('');
  const [state, setState] = useState('');
  const [visibleMacs, setVisibleMacs] = useState(null); // null = show everything

  const [selectedMac, setSelectedMac] = useState(null);
  const [notice, setNotice] = useState(null);
  const [suggestionData, setSuggestionData] = useState(null);
  const [commands, setCommands] = useState([]);

  // Ask the API which devices match the filters. Only a pressed "Search" is logged (log = true).
  const runQuery = useCallback((q, log) => {
    api.getDevices({ q, category, state, log })
      .then((list) => setVisibleMacs(new Set(list.map((d) => d.mac))))
      .catch(() => {});
  }, [category, state]);

  useEffect(() => {
    runQuery(appliedText, false);
    const timer = setInterval(() => runQuery(appliedText, false), 3000);
    return () => clearInterval(timer);
  }, [runQuery, appliedText]);

  const refreshSide = useCallback(() => {
    api.getSuggestions().then(setSuggestionData).catch(() => {});
    api.getCommandHistory().then(setCommands).catch(() => {});
  }, []);

  useEffect(() => {
    refreshSide();
    const timer = setInterval(refreshSide, 2000);
    return () => clearInterval(timer);
  }, [refreshSide]);

  const search = () => {
    const trimmed = text.trim();
    setAppliedText(trimmed);
    runQuery(trimmed, true);
  };

  // Runs an API call and shows its result message (or the error) in the notice bar.
  const run = async (action) => {
    try {
      const result = await action();
      setNotice({ ok: true, text: result.message });
    } catch (err) {
      setNotice({ ok: false, text: err.message });
    }
    refreshSide();
  };

  const onCommand = (mac, action) => run(() => api.sendCommand(mac, action));

  const onSimulate = (kind, mac) => {
    if (kind === 'spike') return run(() => api.simulateSpike(mac));
    if (kind === 'drift') return run(() => api.simulateDrift(mac));
    if (kind === 'disconnect') return run(() => api.simulateDisconnect(mac));
    return run(() => api.simulateReconnect(mac));
  };

  const applySuggestion = (s) => {
    if (s.kind === 'search') {
      const match = s.title.match(/"(.+)"/);
      if (match) {
        setText(match[1]);
        setAppliedText(match[1]);
        runQuery(match[1], false);
      }
      return;
    }
    if (s.mac === 'ALL') {
      run(() => api.shutDownAll());
    } else {
      run(() => api.sendCommand(s.mac, s.turnOn ? 'On' : 'Off'));
    }
  };

  const visibleDevices = Object.values(devices)
    .filter((d) => !visibleMacs || visibleMacs.has(d.mac))
    .sort((a, b) => a.nodeId.localeCompare(b.nodeId));

  const selected = selectedMac ? devices[selectedMac] : null;

  return (
    <main className="cs-page">
      <div className="panel">
        <h2>Queue Processing</h2>
        <QueueStats />
      </div>

      {notice && (
        <div className={`notice ${notice.ok ? 'ok' : 'bad'}`} onClick={() => setNotice(null)}>{notice.text}</div>
      )}

      <div className="cs-layout">
        <div className="cs-main">
          <div className="panel">
            <h2>Devices ({visibleDevices.length})</h2>
            <FilterBar
              text={text} onTextChange={setText} onSearch={search}
              category={category} onCategoryChange={setCategory}
              state={state} onStateChange={setState}
            />
            <div className="device-grid">
              {visibleDevices.map((d) => (
                <DeviceCard
                  key={d.mac}
                  device={d}
                  selected={d.mac === selectedMac}
                  onSelect={setSelectedMac}
                  onCommand={onCommand}
                  onSimulate={onSimulate}
                />
              ))}
              {visibleDevices.length === 0 && (
                <p className="hint-text">No devices match. Register some on the Sensor Ingestion page (or run the demo seed script from the README).</p>
              )}
            </div>
          </div>

          <div className="panel">
            <h2>Timeline</h2>
            <TimelineChart device={selected} />
          </div>
        </div>

        <div className="cs-side">
          <div className="panel">
            <h2>Predictive Engine</h2>
            <SuggestionsPanel data={suggestionData} onApply={applySuggestion} />
          </div>

          <div className="panel">
            <h2>Override History</h2>
            <CommandHistory
              commands={commands}
              onUndo={() => run(() => api.undoCommand())}
              onShutDownAll={() => run(() => api.shutDownAll())}
            />
          </div>

          <div className="panel">
            <h2>Active Faults</h2>
            <FaultList />
          </div>
        </div>
      </div>
    </main>
  );
}

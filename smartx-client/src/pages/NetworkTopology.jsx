import React, { useCallback, useEffect, useState } from 'react';
import { api } from '../api/client.js';
import { useTelemetry } from '../context/TelemetryContext.jsx';
import MeshGraphView from '../components/MeshGraphView.jsx';
import RoutePanel from '../components/RoutePanel.jsx';
import NodeControls from '../components/NodeControls.jsx';
import ConnectedPanel from '../components/ConnectedPanel.jsx';
import HistoryLog from '../components/HistoryLog.jsx';
import BenchmarkPanel from '../components/BenchmarkPanel.jsx';
import LinkEditor from '../components/LinkEditor.jsx';

export default function NetworkTopology() {
  const { meshVersion } = useTelemetry();
  const [topology, setTopology] = useState(null);
  const [selectedId, setSelectedId] = useState(null);
  const [route, setRoute] = useState(null);
  const [report, setReport] = useState(null);
  const [notice, setNotice] = useState(null);

  const loadTopology = useCallback(() => {
    api.getTopology().then(setTopology).catch(() => {});
  }, []);

  // refresh now, every 2 seconds, and straight away whenever the API says the mesh changed
  useEffect(() => {
    loadTopology();
    const timer = setInterval(loadTopology, 2000);
    return () => clearInterval(timer);
  }, [loadTopology, meshVersion]);

  // keep the selected node's route up to date
  useEffect(() => {
    if (!selectedId) {
      setRoute(null);
      return;
    }
    api.getRoute(selectedId).then(setRoute).catch(() => {});
  }, [selectedId, topology]);

  const select = (id) => {
    setSelectedId(id);
    setReport(null);
  };

  const traceByText = async (text) => {
    const result = await api.getRoute(text).catch(() => null);
    if (result) {
      setRoute(result);
      if (result.found) {
        setSelectedId(result.id);
        setReport(null);
      }
    }
  };

  const failNode = async (id) => {
    setReport(await api.failNode(id));
    loadTopology();
  };

  const recoverNode = async (id) => {
    setReport(await api.recoverNode(id));
    loadTopology();
  };

  if (!topology) {
    return <main className="cs-page"><p className="hint-text">Loading the network…</p></main>;
  }

  const devices = topology.nodes.filter((n) => !n.isGateway);
  const zones = [...new Set(devices.map((n) => n.zone))].sort();
  const selectedNode = topology.nodes.find((n) => n.id === selectedId);
  const saving = topology.mst.allLinksCost > 0
    ? Math.round((1 - topology.mst.totalCost / topology.mst.allLinksCost) * 100)
    : 0;

  return (
    <main className="cs-page">
      <div className="panel">
        <h2>Mesh Network</h2>
        <div className="stat-row">
          <div className="stat"><span className="stat-label">Nodes</span><strong>{topology.nodes.length}</strong></div>
          <div className="stat"><span className="stat-label">Wireless links</span><strong>{topology.links.length}</strong></div>
          <div className="stat"><span className="stat-label">MST power cost</span><strong>{topology.mst.totalCost}</strong></div>
          <div className="stat"><span className="stat-label">If every link was used</span><strong>{topology.mst.allLinksCost}</strong></div>
          <div className="stat priority"><span className="stat-label">Power saved by MST</span><strong>{saving}%</strong></div>
          <div className="stat"><span className="stat-label">Down / cut off</span><strong>{topology.down.length} / {topology.isolated.length}</strong></div>
        </div>
      </div>

      {notice && (
        <div className={`notice ${notice.ok ? 'ok' : 'bad'}`} onClick={() => setNotice(null)}>{notice.text}</div>
      )}

      {devices.length === 0 && (
        <div className="notice bad">No devices yet. Register some on the Sensor Ingestion page (or run the seed script from the README).</div>
      )}

      <div className="cs-layout">
        <div className="cs-main">
          <div className="panel">
            <h2>Topology</h2>
            <MeshGraphView topology={topology} selectedId={selectedId} route={route} onSelect={select} />
            <div className="legend">
              <span><i className="swatch mst" /> MST link</span>
              <span><i className="swatch route" /> selected route</span>
              <span><i className="swatch plain" /> unused link</span>
              <span><i className="swatch down" /> down</span>
            </div>
          </div>

          <div className="panel">
            <h2>Historical Telemetry Log</h2>
            <HistoryLog nodeId={selectedId} label={selectedNode ? selectedNode.nodeId : ''} />
          </div>
        </div>

        <div className="cs-side">
          <div className="panel">
            <h2>Route Tracker</h2>
            <RoutePanel nodes={topology.nodes} route={route} onTrace={traceByText} />
          </div>

          <div className="panel">
            <h2>Failure and Self-Healing</h2>
            <NodeControls node={selectedNode} report={report} onFail={failNode} onRecover={recoverNode} />
          </div>

          <div className="panel">
            <h2>Connected Devices (BFS / DFS)</h2>
            <ConnectedPanel zones={zones} />
          </div>

          <div className="panel">
            <h2>Edit Links</h2>
            <LinkEditor nodes={topology.nodes} onChanged={loadTopology} onMessage={setNotice} />
          </div>

          <div className="panel">
            <h2>AVL Tree Benchmark</h2>
            <BenchmarkPanel />
          </div>
        </div>
      </div>
    </main>
  );
}

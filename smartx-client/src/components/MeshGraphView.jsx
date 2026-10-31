import React from 'react';

const WIDTH = 720;
const HEIGHT = 460;
const CENTER = { x: 360, y: 230 };

const pairKey = (a, b) => (a < b ? `${a}|${b}` : `${b}|${a}`);

// Works out where each node sits. The gateway is in the middle and the devices are spaced evenly
// around it in a ring (sorted by zone then name, so the picture stays the same between updates).
function layout(nodes) {
  const positions = {};
  const devices = nodes
    .filter((n) => !n.isGateway)
    .sort((a, b) => a.zone.localeCompare(b.zone) || a.nodeId.localeCompare(b.nodeId));

  nodes.filter((n) => n.isGateway).forEach((n) => { positions[n.id] = CENTER; });

  devices.forEach((n, i) => {
    const angle = -Math.PI / 2 + (2 * Math.PI * i) / Math.max(devices.length, 1);
    positions[n.id] = {
      x: CENTER.x + 270 * Math.cos(angle),
      y: CENTER.y + 170 * Math.sin(angle)
    };
  });

  return positions;
}

/**
 * Draws the mesh as an SVG graph.
 *  - green thick lines  = links chosen by Prim's minimum spanning tree
 *  - orange thick lines = the route of the node you selected
 *  - grey dashed lines  = other links that exist but are not used
 *  - red nodes          = down
 */
export default function MeshGraphView({ topology, selectedId, route, onSelect }) {
  if (!topology) return <p className="hint-text">Loading the mesh…</p>;

  const positions = layout(topology.nodes);
  const nodeById = {};
  topology.nodes.forEach((n) => { nodeById[n.id] = n; });

  // links that make up the selected node's MST route
  const routeLinks = new Set();
  if (route?.found && route.hops.length > 1) {
    for (let i = 0; i < route.hops.length - 1; i += 1) {
      routeLinks.add(pairKey(route.hops[i].id, route.hops[i + 1].id));
    }
  }

  return (
    <svg viewBox={`0 0 ${WIDTH} ${HEIGHT}`} className="mesh-svg" role="img" aria-label="IoT mesh network graph">
      {topology.links.map((link) => {
        const a = positions[link.a];
        const b = positions[link.b];
        if (!a || !b) return null;

        const isRoute = routeLinks.has(pairKey(link.a, link.b));
        const endDown = !nodeById[link.a]?.up || !nodeById[link.b]?.up;
        let className = 'mesh-link';
        if (endDown) className += ' down';
        else if (isRoute) className += ' route';
        else if (link.inMst) className += ' mst';

        return (
          <g key={pairKey(link.a, link.b)}>
            <line x1={a.x} y1={a.y} x2={b.x} y2={b.y} className={className}>
              <title>{`${nodeById[link.a]?.nodeId} ↔ ${nodeById[link.b]?.nodeId}: power cost ${link.cost}`}</title>
            </line>
            {(isRoute || (link.inMst && !endDown)) && (
              <text x={(a.x + b.x) / 2} y={(a.y + b.y) / 2 - 4} className="mesh-cost">{link.cost}</text>
            )}
          </g>
        );
      })}

      {topology.nodes.map((node) => {
        const p = positions[node.id];
        let className = 'mesh-node';
        if (node.isGateway) className += ' gateway';
        else if (!node.up) className += ' down';
        if (node.id === selectedId) className += ' selected';

        return (
          <g key={node.id} className={className} onClick={() => onSelect(node.id)} style={{ cursor: 'pointer' }}>
            <circle cx={p.x} cy={p.y} r={node.isGateway ? 26 : 20} />
            <text x={p.x} y={p.y + 4} className="mesh-node-icon">{node.isGateway ? 'GW' : (node.up ? '●' : '✕')}</text>
            <text x={p.x} y={p.y + (node.isGateway ? 44 : 38)} className="mesh-node-label">{node.nodeId}</text>
          </g>
        );
      })}
    </svg>
  );
}

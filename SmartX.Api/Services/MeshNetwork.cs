using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// The IoT mesh network, modelled as a graph.
///   - Vertices (nodes) are the registered ESP32 devices, plus one special vertex for the central gateway.
///   - Edges are the wireless links between them. Each edge has a cost: the power needed to send a
///     packet over that link (lower is better).
/// A node counts as "down" if it was manually failed from the dashboard, or if the Part 2 watchdog has
/// marked its device Disconnected. Down nodes are skipped by every algorithm, so routes heal around them.
///
/// Links between devices are generated when a device registers: it links to its cheapest nearby nodes
/// (same zone is cheaper than a different zone), so the network has loops and the MST has real work to do.
/// </summary>
public class MeshNetwork
{
    public const string GatewayId = "GATEWAY";

    private readonly Dictionary<string, List<MeshEdge>> _graph = new Dictionary<string, List<MeshEdge>>();
    private readonly HashSet<string> _failed = new HashSet<string>();
    private readonly object _lock = new object();

    private readonly DeviceRegistry _devices;
    private readonly SimulatorControls _controls;

    public MeshNetwork(DeviceRegistry devices, SimulatorControls controls)
    {
        _devices = devices;
        _controls = controls;
        _graph[GatewayId] = new List<MeshEdge>();
    }

    // ------------------------------------------------------------------ building the graph

    /// <summary>Adds a new device as a vertex and links it to its cheapest neighbours.</summary>
    public void AddDevice(DeviceInfo device)
    {
        lock (_lock)
        {
            if (_graph.ContainsKey(device.Mac)) return;

            // work out the cost of linking to every node that already exists
            var candidates = new List<(string Id, double Cost)>();
            foreach (var existingId in _graph.Keys)
            {
                candidates.Add((existingId, EstimateCost(device, existingId)));
            }

            _graph[device.Mac] = new List<MeshEdge>();

            // In a mesh, devices mostly talk to NEIGHBOURING devices, not straight to the gateway.
            // So link to the 2 cheapest other devices (same zone is cheapest)...
            var others = candidates.Where(c => c.Id != GatewayId).OrderBy(c => c.Cost).ToList();
            var links = others.Take(2).ToList();

            // ...some devices get one extra link, chosen from the MAC so it is the same every run...
            if (StableHash(device.Mac) % 3 == 0 && others.Count > 2)
            {
                links.Add(others[2]);
            }

            // ...and the first device in each zone also gets an uplink to the gateway.
            var firstInZone = !_devices.GetAll().Any(d => d.Zone == device.Zone && d.Mac != device.Mac);
            if (links.Count == 0 || firstInZone)
            {
                links.Add(candidates.First(c => c.Id == GatewayId));
            }

            foreach (var link in links)
            {
                ConnectUnlocked(device.Mac, link.Id, link.Cost);
            }
        }
    }

    public string? AddLink(string a, string b, double cost)
    {
        lock (_lock)
        {
            var idA = ResolveUnlocked(a);
            var idB = ResolveUnlocked(b);

            if (idA == null || idB == null) return "Both ends of the link must be known devices (or GATEWAY).";
            if (idA == idB) return "A node cannot link to itself.";
            if (cost <= 0) return "The power cost must be greater than 0.";
            if (LinkExistsUnlocked(idA, idB)) return "Those two nodes are already linked.";

            ConnectUnlocked(idA, idB, cost);
            return null;
        }
    }

    public string? RemoveLink(string a, string b)
    {
        lock (_lock)
        {
            var idA = ResolveUnlocked(a);
            var idB = ResolveUnlocked(b);

            if (idA == null || idB == null || !LinkExistsUnlocked(idA, idB)) return "No such link.";

            _graph[idA].RemoveAll(e => e.To == idB);
            _graph[idB].RemoveAll(e => e.To == idA);
            return null;
        }
    }

    private void ConnectUnlocked(string a, string b, double cost)
    {
        // a wireless link works both ways, so the edge is stored on both nodes (undirected graph)
        _graph[a].Add(new MeshEdge(b, cost));
        _graph[b].Add(new MeshEdge(a, cost));
    }

    private bool LinkExistsUnlocked(string a, string b)
    {
        return _graph[a].Any(e => e.To == b);
    }

    private double EstimateCost(DeviceInfo newDevice, string existingId)
    {
        double baseCost;

        if (existingId == GatewayId)
        {
            baseCost = 14;
        }
        else if (_devices.TryGet(existingId, out var other) && other != null && other.Zone == newDevice.Zone)
        {
            baseCost = 6;   // same zone: short hop, low power
        }
        else
        {
            baseCost = 22;  // different zone: long hop, more power
        }

        // a small repeatable difference so links are not all identical
        var pair = string.CompareOrdinal(newDevice.Mac, existingId) < 0
            ? newDevice.Mac + "|" + existingId
            : existingId + "|" + newDevice.Mac;

        return baseCost + (StableHash(pair) % 10);
    }

    // FNV-1a hash. string.GetHashCode() changes every run, and I want the same links every time.
    private static int StableHash(string text)
    {
        unchecked
        {
            uint hash = 2166136261;
            foreach (var ch in text)
            {
                hash = (hash ^ ch) * 16777619;
            }
            return (int)(hash & 0x7FFFFFFF);
        }
    }

    // ------------------------------------------------------------------ node state

    public bool IsUp(string id)
    {
        if (id == GatewayId) return true;
        lock (_lock)
        {
            return IsUpUnlocked(id);
        }
    }

    private bool IsUpUnlocked(string id)
    {
        if (id == GatewayId) return true;
        if (_failed.Contains(id)) return false;

        if (_devices.TryGet(id, out var device) && device != null)
        {
            return device.Status != "Disconnected";
        }
        return false;
    }

    /// <summary>Accepts a MAC address, a node id like "pump-a", or "GATEWAY" and returns the graph id.</summary>
    public string? Resolve(string idOrMac)
    {
        lock (_lock)
        {
            return ResolveUnlocked(idOrMac);
        }
    }

    private string? ResolveUnlocked(string idOrMac)
    {
        if (string.IsNullOrWhiteSpace(idOrMac)) return null;

        var text = idOrMac.Trim();
        if (text.Equals(GatewayId, StringComparison.OrdinalIgnoreCase)) return GatewayId;

        var mac = DeviceRegistry.NormaliseMac(text);
        if (_graph.ContainsKey(mac)) return mac;

        var byName = _devices.GetAll().FirstOrDefault(d => d.NodeId.Equals(text, StringComparison.OrdinalIgnoreCase));
        if (byName != null && _graph.ContainsKey(byName.Mac)) return byName.Mac;

        return null;
    }

    private string LabelUnlocked(string id)
    {
        if (id == GatewayId) return "Gateway";
        return _devices.TryGet(id, out var device) && device != null ? device.NodeId : id;
    }

    // ------------------------------------------------------------------ the whole picture

    public TopologyDto GetTopology()
    {
        lock (_lock)
        {
            var mst = MeshAlgorithms.Prim(_graph, GatewayId, IsUpUnlocked);
            var mstKeys = new HashSet<string>(mst.Edges.Select(e => PairKey(e.From, e.To)));

            // nodes
            var nodes = new List<MeshNodeDto>
            {
                new MeshNodeDto(GatewayId, "Gateway", "-", "Gateway", true, true, false, "Online")
            };
            foreach (var id in _graph.Keys.Where(k => k != GatewayId))
            {
                _devices.TryGet(id, out var device);
                nodes.Add(new MeshNodeDto(
                    id,
                    device != null ? device.NodeId : id,
                    device != null ? device.Zone : "?",
                    device != null ? device.Category : "?",
                    false,
                    IsUpUnlocked(id),
                    _failed.Contains(id),
                    device != null ? device.Status : "Unknown"));
            }

            // links (each undirected link is listed once)
            var links = new List<MeshLinkDto>();
            var seen = new HashSet<string>();
            double allLinksCost = 0;
            foreach (var pair in _graph)
            {
                foreach (var edge in pair.Value)
                {
                    var key = PairKey(pair.Key, edge.To);
                    if (!seen.Add(key)) continue;

                    links.Add(new MeshLinkDto(pair.Key, edge.To, edge.Cost, mstKeys.Contains(key)));
                    if (IsUpUnlocked(pair.Key) && IsUpUnlocked(edge.To)) allLinksCost += edge.Cost;
                }
            }

            // BFS from the gateway: who can still reach it?
            var reachable = MeshAlgorithms.Bfs(_graph, GatewayId, IsUpUnlocked).Select(r => r.Id).ToList();
            var down = _graph.Keys.Where(k => !IsUpUnlocked(k)).ToList();
            var isolated = _graph.Keys.Where(k => IsUpUnlocked(k) && !reachable.Contains(k)).ToList();

            var mstDto = new MstDto(
                Math.Round(mst.TotalCost, 1),
                Math.Round(allLinksCost, 1),
                mst.Edges.Select(e => new MeshLinkDto(e.From, e.To, e.Cost, true)).ToList(),
                isolated);

            return new TopologyDto(nodes, links, mstDto, reachable, isolated, down);
        }
    }

    private static string PairKey(string a, string b)
    {
        return string.CompareOrdinal(a, b) < 0 ? a + "|" + b : b + "|" + a;
    }

    // ------------------------------------------------------------------ routing

    /// <summary>The power-efficient route (along the minimum spanning tree) from one node to the gateway.</summary>
    public RouteDto GetRoute(string idOrMac)
    {
        lock (_lock)
        {
            var id = ResolveUnlocked(idOrMac);
            if (id == null)
            {
                return new RouteDto(idOrMac, idOrMac, false, "No device with that MAC address or node id.", new List<RouteHop>(), 0, 0, null, new List<RouteHop>(), 0);
            }

            var label = LabelUnlocked(id);
            if (id == GatewayId)
            {
                return new RouteDto(id, label, true, "This is the gateway itself.", new List<RouteHop> { new RouteHop(id, label) }, 0, 0, null, new List<RouteHop>(), 0);
            }

            if (!IsUpUnlocked(id))
            {
                return new RouteDto(id, label, false, label + " is down, so it has no route.", new List<RouteHop>(), 0, 0, null, new List<RouteHop>(), 0);
            }

            var mst = MeshAlgorithms.Prim(_graph, GatewayId, IsUpUnlocked);
            var path = MeshAlgorithms.PathToRoot(mst.Parent, id, GatewayId);
            if (path == null)
            {
                return new RouteDto(id, label, false, label + " is cut off from the gateway (every path to it is down).", new List<RouteHop>(), 0, 0, null, new List<RouteHop>(), 0);
            }

            // the cheapest single path for this node on its own (Dijkstra), to compare with the MST route
            var shortestParent = MeshAlgorithms.Dijkstra(_graph, GatewayId, IsUpUnlocked);
            var shortest = MeshAlgorithms.PathToRoot(shortestParent, id, GatewayId) ?? path;

            return new RouteDto(
                id,
                label,
                true,
                "Route found.",
                path.Select(p => new RouteHop(p, LabelUnlocked(p))).ToList(),
                Math.Round(PathCostUnlocked(path), 1),
                path.Count - 1,
                MeshAlgorithms.LinkCost(_graph, id, GatewayId),
                shortest.Select(p => new RouteHop(p, LabelUnlocked(p))).ToList(),
                Math.Round(PathCostUnlocked(shortest), 1));
        }
    }

    private double PathCostUnlocked(List<string> path)
    {
        double total = 0;
        for (var i = 0; i < path.Count - 1; i++)
        {
            total += MeshAlgorithms.LinkCost(_graph, path[i], path[i + 1]) ?? 0;
        }
        return total;
    }

    // ------------------------------------------------------------------ traversal

    /// <summary>Which devices are connected to the gateway (or to another start node), using BFS or DFS.</summary>
    public ConnectedDto GetConnected(string? from, string? zone, string mode)
    {
        lock (_lock)
        {
            var start = string.IsNullOrWhiteSpace(from) ? GatewayId : (ResolveUnlocked(from) ?? GatewayId);
            var useDfs = mode.Equals("dfs", StringComparison.OrdinalIgnoreCase);

            var visited = useDfs
                ? MeshAlgorithms.Dfs(_graph, start, IsUpUnlocked)
                : MeshAlgorithms.Bfs(_graph, start, IsUpUnlocked);

            var order = new List<ConnectedNode>();
            foreach (var entry in visited)
            {
                _devices.TryGet(entry.Id, out var device);
                var nodeZone = entry.Id == GatewayId ? "-" : (device != null ? device.Zone : "?");

                // a "subnet" here is a zone: the search can pass through other zones to get there
                if (!string.IsNullOrWhiteSpace(zone) && entry.Id != GatewayId &&
                    !nodeZone.Equals(zone, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                order.Add(new ConnectedNode(entry.Id, LabelUnlocked(entry.Id), nodeZone, entry.Hops));
            }

            var reached = new HashSet<string>(visited.Select(v => v.Id));
            var down = _graph.Keys.Where(k => !IsUpUnlocked(k)).Select(LabelUnlocked).ToList();
            var isolated = _graph.Keys.Where(k => IsUpUnlocked(k) && !reached.Contains(k)).Select(LabelUnlocked).ToList();

            return new ConnectedDto(useDfs ? "DFS" : "BFS", LabelUnlocked(start), zone, order, down, isolated);
        }
    }

    // ------------------------------------------------------------------ failure and healing

    public RerouteReport Fail(string idOrMac)
    {
        RerouteReport report;
        string failedId;

        lock (_lock)
        {
            var id = ResolveUnlocked(idOrMac);
            if (id == null || id == GatewayId)
            {
                return new RerouteReport("fail", idOrMac, "Unknown device (the gateway cannot be failed).", 0, 0, new List<RerouteEntry>());
            }
            if (_failed.Contains(id))
            {
                return new RerouteReport("fail", LabelUnlocked(id), LabelUnlocked(id) + " is already down.", 0, 0, new List<RerouteEntry>());
            }

            failedId = id;

            var before = MeshAlgorithms.Prim(_graph, GatewayId, IsUpUnlocked);
            _failed.Add(id);
            var after = MeshAlgorithms.Prim(_graph, GatewayId, IsUpUnlocked);

            report = BuildReport("fail", id, before, after);
        }

        // make the simulated device stop reporting too, like an unplugged ESP32
        _controls.Silence(failedId);
        return report;
    }

    public RerouteReport Recover(string idOrMac)
    {
        RerouteReport report;
        string recoveredId;

        lock (_lock)
        {
            var id = ResolveUnlocked(idOrMac);
            if (id == null || id == GatewayId)
            {
                return new RerouteReport("recover", idOrMac, "Unknown device.", 0, 0, new List<RerouteEntry>());
            }

            recoveredId = id;

            var before = MeshAlgorithms.Prim(_graph, GatewayId, IsUpUnlocked);
            _failed.Remove(id);
            var after = MeshAlgorithms.Prim(_graph, GatewayId, IsUpUnlocked);

            report = BuildReport("recover", id, before, after);
        }

        _controls.Resume(recoveredId);
        return report;
    }

    /// <summary>Compares the routes before and after a change and lists every node whose route is different.</summary>
    private RerouteReport BuildReport(string action, string changedId, MstResult before, MstResult after)
    {
        var changes = new List<RerouteEntry>();

        var everyone = new HashSet<string>(before.Included);
        everyone.UnionWith(after.Included);

        foreach (var id in everyone)
        {
            if (id == GatewayId || id == changedId) continue;

            var oldPath = before.Included.Contains(id) ? MeshAlgorithms.PathToRoot(before.Parent, id, GatewayId) : null;
            var newPath = after.Included.Contains(id) ? MeshAlgorithms.PathToRoot(after.Parent, id, GatewayId) : null;

            var same = oldPath != null && newPath != null && oldPath.SequenceEqual(newPath);
            if (same) continue;

            changes.Add(new RerouteEntry(
                id,
                LabelUnlocked(id),
                oldPath == null ? new List<string>() : oldPath.Select(LabelUnlocked).ToList(),
                newPath == null ? new List<string>() : newPath.Select(LabelUnlocked).ToList(),
                newPath != null && newPath.Count > 1 ? LabelUnlocked(newPath[1]) : "-",
                oldPath == null ? 0 : Math.Round(PathCostUnlocked(oldPath), 1),
                newPath == null ? 0 : Math.Round(PathCostUnlocked(newPath), 1),
                newPath == null));
        }

        var name = LabelUnlocked(changedId);
        var lost = changes.Count(c => c.Isolated);
        string summary;
        if (action == "fail")
        {
            summary = changes.Count == 0
                ? name + " is down. No other node used it, so no routes changed."
                : name + " is down. " + changes.Count + " node(s) changed route" + (lost > 0 ? ", " + lost + " cut off." : ".");
        }
        else
        {
            summary = changes.Count == 0
                ? name + " is back. No routes changed."
                : name + " is back. " + changes.Count + " node(s) changed route.";
        }

        return new RerouteReport(action, name, summary, Math.Round(before.TotalCost, 1), Math.Round(after.TotalCost, 1), changes);
    }
}

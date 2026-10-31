# Smart-X IoT Mesh Ecosystem — Final PoE: Network Topology and Mesh Routing

PROG7312 / AAPD7112 — Portfolio of Evidence, Part 3 (the full functioning application).

The Smart-X gateway now has all three pillars in one dashboard. A simulated ESP32 mesh sends telemetry to a .NET 10 API,
which queues, processes and stores it. The React dashboard shows the live feed (Part 1), the real-time command stream with
undo and predictions (Part 2), and, new in Part 3, the **mesh network as a graph** with route tracking, failure
simulation and an **AVL-tree-backed history log**.

**Stack:** .NET 10 ASP.NET Core Minimal API (backend) + React with Vite (frontend) + SignalR (live updates).

---

## 1. The three pillars

| Pillar | Page | Main data structures | Built in |
|---|---|---|---|
| Sensor Data Ingestion and Telemetry | `#/ingestion` | generics, operator overloading, jagged arrays, recursion | Part 1 |
| Real-Time Command Stream and History | `#/commands` | Queue, PriorityQueue, Stack, Dictionary, SortedDictionary, HashSet | Part 2 |
| **Network Topology and Mesh Routing** | `#/topology` | **AVL tree, graph, BFS/DFS, min-heap, Prim's MST** | **Part 3** |

## 2. How it works

```
 Simulated devices                          ASP.NET Core API                                     Browser
 (real HTTP POST, like an ESP32)
        |  POST /api/telemetry
        +--------------------------------> Dictionary lookup by MAC -> Queue / PriorityQueue
                                                          |
                                                  TelemetryProcessor (background worker)
                                                          |-- device state (Dictionary), history (SortedDictionary), faults (HashSet)
                                                          |-- AVL tree per sensor (TelemetryArchive)        <-- Part 3
                                                          '-- SignalR hub --------------------------------> live dashboard

 Registered devices --> MeshNetwork graph --> BFS / DFS, Prim's MST (min-heap), Dijkstra     <-- Part 3
                                                          '-- /api/mesh/* ---------------------------------> Network Topology page
```

The devices are a background service that sends **real HTTP requests** to the API, so the API cannot tell them apart from
hardware. Every registered device automatically becomes a vertex of the mesh graph.

## 3. Project structure

```
SmartX/
├── docker-compose.yml
├── FEEDBACK-UPDATES.md                 updates made from lecturer feedback on Parts 1 and 2
├── scripts/seed-demo-devices.ps1       registers six demo devices
├── SmartX.Api/                         .NET 10 backend
│   ├── Endpoints/                      Telemetry, Command, Simulator and Mesh (Part 3) endpoints
│   ├── Models/                         Part 1 and 2 models + MeshModels.cs (Part 3)
│   └── Services/
│       ├── AvlTree.cs                  self-balancing tree                     (Part 3)
│       ├── TelemetryArchive.cs         one AVL tree per sensor                 (Part 3)
│       ├── MinHeap.cs                  hand-written binary min-heap            (Part 3)
│       ├── MeshAlgorithms.cs           BFS, DFS, Prim's MST, Dijkstra          (Part 3)
│       ├── MeshNetwork.cs              the graph, routes, failures, healing    (Part 3)
│       ├── ArchiveBenchmark.cs         AVL tree vs plain list                  (Part 3)
│       └── ... Part 1 and 2 services (queues, registry, fault tracker, predictions, simulator)
└── smartx-client/                      React dashboard
    └── src/
        ├── context/TelemetryContext.jsx   one shared SignalR connection and state (survives navigation)
        ├── pages/                         Landing, IngestionDashboard, CommandStream, NetworkTopology
        └── components/                    MeshGraphView, RoutePanel, NodeControls, HistoryLog, ConnectedPanel,
                                           BenchmarkPanel, LinkEditor + Part 1 and 2 components
```

## 4. Prerequisites

- .NET 10 SDK
- Node.js 18 or newer (npm comes with it)
- Docker Desktop (optional, only for the Docker option)

## 5. Compile, run and use

### Option A: manually (three terminals)

**Terminal 1, backend:**
```powershell
cd SmartX.Api
dotnet restore
dotnet build
dotnet run
```
Wait for `Now listening on: http://localhost:5080`.

**Terminal 2, frontend:**
```powershell
cd smartx-client
npm install
npm run dev
```
Open `http://localhost:5173`.

**Terminal 3, register the demo devices** (about 10 seconds after the API starts):
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\seed-demo-devices.ps1
```
(`-ExecutionPolicy Bypass` is only needed if Windows says running scripts is disabled.) The simulated devices start
reporting about 4 seconds after the API starts, then every 2 seconds. Restarting the API clears all data, so run the seed
script again after a restart.

### Option B: Docker Compose (one command)

```powershell
docker compose up --build
```
Dashboard: `http://localhost:5173`, API: `http://localhost:5080`. Then run the seed script above.

### Using the application

1. **Home / menu:** all three pillars are active and linked from the top navigation. Navigating between pages keeps the
   live data, because the connection and state live above the router.
2. **Sensor Ingestion:** register a device and attach a file. The new device immediately appears in the mesh graph.
3. **Command Stream:** watch queues, send override commands, undo them, search devices and read the suggestions.
4. **Network Topology (Part 3):** see the mesh, click a node to trace its route and read its history, simulate a failure,
   list connected devices with BFS or DFS, edit links, and run the AVL benchmark.

---

## 6. Part 3 data structures explained

### 6.1 AVL tree: historical telemetry queries

**The problem.** The brief asks for queries like "all moisture readings between 14:00 and 16:00 for Node A" **without
iterating through a massive, unorganised list**. A sensor reporting every 2 seconds produces over 40,000 readings a day.

**The solution.** Each sensor has its own AVL tree (`AvlTree<long, TelemetryPacket<float>>`), keyed by timestamp ticks.
An AVL tree is a binary search tree that keeps itself balanced: every node stores its height, and its *balance factor*
(left height minus right height) must always be -1, 0 or +1. If an insert breaks that rule, the tree rotates itself back
into shape. Without this, readings that arrive in increasing time order would turn a plain binary search tree into a
linked list of height n. With it, the height stays near log2(n) (Adelson-Velsky and Landis, 1962; Cormen et al., 2022).

Rebalancing after an insert (`AvlTree.cs`):

```csharp
private Node Rebalance(Node node)
{
    var balance = BalanceFactor(node);

    if (balance > 1) // left side too heavy
    {
        // Left-Right case: turn it into a Left-Left case first
        if (BalanceFactor(node.Left!) < 0)
        {
            node.Left = RotateLeft(node.Left!);
        }
        return RotateRight(node);
    }

    if (balance < -1) // right side too heavy
    {
        // Right-Left case: turn it into a Right-Right case first
        if (BalanceFactor(node.Right!) > 0)
        {
            node.Right = RotateRight(node.Right!);
        }
        return RotateLeft(node);
    }

    return node; // already balanced
}
```

A rotation changes which node is the parent, without breaking the sorted order:

```csharp
//        y                x
//       / \              / \
//      x   C    --->    A   y
//     / \                  / \
//    A   B                B   C
private Node RotateRight(Node y)
{
    var x = y.Left!;
    y.Left = x.Right;
    x.Right = y;
    UpdateHeight(y); // y is now lower, so update it first
    UpdateHeight(x);
    return x;
}
```

**The range query** goes left only if the start of the range is smaller than this node's key, adds the node if it is
inside the range, and goes right only if the end of the range is bigger than this node's key. Whole branches that cannot
contain a match are never visited, so the cost is about **O(log n + k)** (k = number of results). It also counts the
nodes it visited, so the dashboard can prove how little of the tree was used:

```csharp
private void CollectRange(Node? node, TKey low, TKey high, List<TValue> results, ref int visited)
{
    if (node == null) return;
    visited++;

    var compareLow = low.CompareTo(node.Key);   // < 0 means low is smaller than this key
    var compareHigh = high.CompareTo(node.Key); // > 0 means high is bigger than this key

    // Only go left if there could be keys smaller than this one that are still >= low.
    if (compareLow < 0)
    {
        CollectRange(node.Left, low, high, results, ref visited);
    }

    if (compareLow <= 0 && compareHigh >= 0)
    {
        results.Add(node.Value);
    }

    // Only go right if there could be keys bigger than this one that are still <= high.
    if (compareHigh > 0)
    {
        CollectRange(node.Right, low, high, results, ref visited);
    }
}
```

**How the archive uses it** (`TelemetryArchive.cs`). Keys must be unique, so a reading with the same timestamp as an
existing one is moved forward by one tick. A lock protects the tree because the background worker writes while API
requests read:

```csharp
public void Add(string mac, TelemetryPacket<float> packet)
{
    lock (_lock)
    {
        if (!_trees.TryGetValue(mac, out var tree))
        {
            tree = new AvlTree<long, TelemetryPacket<float>>();
            _trees[mac] = tree;
        }

        // keys must be unique, so nudge by one tick if two readings share a timestamp
        var key = packet.Timestamp.UtcTicks;
        while (tree.TryGet(key, out _))
        {
            key++;
        }
        tree.Insert(key, packet);
    }
}
```

**Measured performance.** The *AVL Tree Benchmark* panel builds a fake history in random insertion order, then asks
for the same 2-hour window with the tree and with a plain list scan. It also checks that the tree is a valid AVL tree and
that both methods return the same readings. One run (your numbers will differ, the trend will not):

| History size | AVL range query | Nodes visited | Plain list scan | Speed-up | Tree height |
|---|---|---|---|---|---|
| 50,000 | 287.4 µs | 7,215 | 1205.3 µs | 4.2× | 19 |
| 200,000 | 224.3 µs | 7,221 | 4526.6 µs | 20.2× | 21 |
| 500,000 | 250.8 µs | 7,221 | 11137.4 µs | 44.4× | 23 |

The AVL query time stays flat (it mostly returns the ~7,200 matching readings) while the list scan grows with the whole
history. The tree height grows by only 4 levels while the history grows 10 times.

### 6.2 The mesh as a graph

The mesh is an **undirected weighted graph**. Vertices are the registered ESP32 devices plus one vertex for the central
gateway. Edges are wireless links, and the weight is the **power cost** of sending a packet over the link (lower is
better). The graph is an **adjacency list**: a `Dictionary<string, List<MeshEdge>>` from each node to the links leaving
it. A wireless link works both ways, so every link is stored on both of its nodes:

```csharp
private void ConnectUnlocked(string a, string b, double cost)
{
    // a wireless link works both ways, so the edge is stored on both nodes (undirected graph)
    _graph[a].Add(new MeshEdge(b, cost));
    _graph[b].Add(new MeshEdge(a, cost));
}
```

Links are generated when a device registers (a real deployment would measure signal strength): it links to its two
cheapest neighbours (same zone is cheaper than another zone), some devices get a third link chosen from a stable hash
of the MAC address (so the mesh is identical every run), and the first device in each zone also gets an uplink to the
gateway. Links can also be added or removed on the dashboard. A node is **down** if it was failed from the dashboard or
the Part 2 watchdog marked it disconnected. Every algorithm takes an `isUp` function and skips down nodes.

### 6.3 Graph traversal: BFS and DFS

**Breadth-First Search** uses a `Queue`. It visits the start node, then all its neighbours, then their neighbours. Because
it goes level by level, the level number is the smallest number of wireless hops from the start:

```csharp
public static List<(string Id, int Hops)> Bfs(Dictionary<string, List<MeshEdge>> graph, string start, Func<string, bool> isUp)
{
    var order = new List<(string Id, int Hops)>();
    if (!graph.ContainsKey(start) || !isUp(start)) return order;

    var visited = new HashSet<string> { start };
    var queue = new Queue<(string Id, int Hops)>();
    queue.Enqueue((start, 0));

    while (queue.Count > 0)
    {
        var current = queue.Dequeue();
        order.Add(current);

        foreach (var edge in graph[current.Id])
        {
            if (!isUp(edge.To)) continue;
            if (!visited.Add(edge.To)) continue; // Add returns false if it was already visited

            queue.Enqueue((edge.To, current.Hops + 1));
        }
    }

    return order;
}
```

**Depth-First Search** uses a `Stack` and follows one branch as far as it can before backing up. It uses an explicit
stack instead of recursion, so a very long chain of nodes cannot overflow the call stack:

```csharp
public static List<(string Id, int Hops)> Dfs(Dictionary<string, List<MeshEdge>> graph, string start, Func<string, bool> isUp)
{
    var order = new List<(string Id, int Hops)>();
    if (!graph.ContainsKey(start) || !isUp(start)) return order;

    var visited = new HashSet<string>();
    var stack = new Stack<(string Id, int Depth)>();
    stack.Push((start, 0));

    while (stack.Count > 0)
    {
        var current = stack.Pop();
        if (!visited.Add(current.Id)) continue;

        order.Add((current.Id, current.Depth));

        // push in reverse so the first neighbour is explored first
        var neighbours = graph[current.Id];
        for (var i = neighbours.Count - 1; i >= 0; i--)
        {
            var next = neighbours[i].To;
            if (isUp(next) && !visited.Contains(next))
            {
                stack.Push((next, current.Depth + 1));
            }
        }
    }

    return order;
}
```

Both are **O(V + E)**. They answer the brief's connectivity questions: which devices are currently connected to the
gateway, which are connected within one zone (the "subnet" filter: the search may pass through other zones, but only
that zone's devices are listed), and which are **down** or **cut off** (up, but with no path left to the gateway).

### 6.4 Heap and Prim's minimum spanning tree

A **minimum spanning tree (MST)** connects every node using the smallest possible total link cost. For the mesh it is the
cheapest set of links to keep on so every device can still reach the gateway (the most power-efficient routing backbone).
**Prim's algorithm** (Prim, 1957) starts at the gateway and repeatedly adds the cheapest link that joins a node already in
the tree to one that is not. It needs the cheapest link fast, which is what a **heap** gives.

`MinHeap<T>` is a binary min-heap written by hand. It lives in a list, the children of index `i` are at `2i+1` and
`2i+2`, and `Push` and `Pop` each move an item along one path, so both are **O(log n)** (GeeksforGeeks, n.d.c):

```csharp
public (T Item, double Priority) Pop()
{
    if (_data.Count == 0)
    {
        throw new InvalidOperationException("The heap is empty.");
    }

    var top = _data[0];
    var last = _data[_data.Count - 1];
    _data.RemoveAt(_data.Count - 1);

    if (_data.Count > 0)
    {
        _data[0] = last;   // move the last item to the top...
        SiftDown(0);       // ...and let it sink to where it belongs
    }

    return top;
}
```

```csharp
private void SiftDown(int index)
{
    var count = _data.Count;

    while (true)
    {
        var left = 2 * index + 1;
        var right = left + 1;
        var smallest = index;

        if (left < count && _data[left].Priority < _data[smallest].Priority) smallest = left;
        if (right < count && _data[right].Priority < _data[smallest].Priority) smallest = right;

        if (smallest == index) break;

        Swap(index, smallest);
        index = smallest;
    }
}
```

Prim's algorithm using the heap (**O(E log E)**):

```csharp
public static MstResult Prim(Dictionary<string, List<MeshEdge>> graph, string root, Func<string, bool> isUp)
{
    var result = new MstResult();
    if (!graph.ContainsKey(root) || !isUp(root)) return result;

    var heap = new MinHeap<MstEdge>();
    result.Included.Add(root);

    foreach (var edge in graph[root])
    {
        if (isUp(edge.To))
        {
            heap.Push(new MstEdge(root, edge.To, edge.Cost), edge.Cost);
        }
    }

    while (heap.Count > 0)
    {
        var cheapest = heap.Pop().Item;

        // the heap can still hold old links to nodes we have since reached another way
        if (result.Included.Contains(cheapest.To)) continue;

        result.Included.Add(cheapest.To);
        result.Edges.Add(cheapest);
        result.Parent[cheapest.To] = cheapest.From;
        result.TotalCost += cheapest.Cost;

        foreach (var edge in graph[cheapest.To])
        {
            if (isUp(edge.To) && !result.Included.Contains(edge.To))
            {
                heap.Push(new MstEdge(cheapest.To, edge.To, edge.Cost), edge.Cost);
            }
        }
    }

    return result;
}
```

Old heap entries (links to nodes already reached another way) are skipped when they come out. If some nodes cannot be
reached, the loop ends without them and they are reported as cut off. For the six seed devices (13 links) the MST keeps
6 links at a total power cost of **82**, against **231** if every link were used, which is about **65% less power**.

### 6.5 Dijkstra's shortest path, shown next to the MST

A node's MST route minimises the cost of the **whole network**, not the cost for **one node**. For `pump-a` the MST route
costs 50 over 3 hops, while its cheapest possible single path costs 29 over 2 hops. That is not a bug, so the route panel
shows both. Dijkstra's algorithm (Dijkstra, 1959) uses the same heap:

```csharp
public static Dictionary<string, string> Dijkstra(Dictionary<string, List<MeshEdge>> graph, string root, Func<string, bool> isUp)
{
    var parent = new Dictionary<string, string>();
    if (!graph.ContainsKey(root) || !isUp(root)) return parent;

    var best = new Dictionary<string, double> { [root] = 0 };
    var done = new HashSet<string>();
    var heap = new MinHeap<string>();
    heap.Push(root, 0);

    while (heap.Count > 0)
    {
        var current = heap.Pop();
        if (!done.Add(current.Item)) continue; // already settled with a cheaper cost

        foreach (var edge in graph[current.Item])
        {
            if (!isUp(edge.To) || done.Contains(edge.To)) continue;

            var newCost = current.Priority + edge.Cost;
            if (!best.TryGetValue(edge.To, out var oldCost) || newCost < oldCost)
            {
                best[edge.To] = newCost;
                parent[edge.To] = current.Item;
                heap.Push(edge.To, newCost);
            }
        }
    }

    return parent;
}
```

### 6.6 Failure and self-healing

`MeshNetwork.Fail` computes the MST before the failure, marks the node down, computes it again, and compares every node's
route. The report lists each node whose route changed with its **new next hop** (where to hop when a device is down), or
marks it cut off. With the seed devices, failing `moisture-sensor-a` re-routes 5 nodes and the tree cost drops from 82 to
78, and failing `power-meter-2` as well cuts off 4 nodes. The failed device also stops reporting in the simulator, like an
unplugged board.

### 6.7 Complexity summary

| Structure or algorithm | Used for | Cost |
|---|---|---|
| AVL tree | per-sensor history, range queries | insert and find O(log n); range query O(log n + k) |
| Adjacency list | the mesh graph | space O(V + E) |
| BFS / DFS | connected devices, hop counts, cut-off detection | O(V + E) |
| Min-heap | link priority in Prim's and Dijkstra | push and pop O(log n) |
| Prim's MST | power-efficient backbone and routes | O(E log E) |
| Dijkstra | cheapest single path (comparison) | O(E log V) |

---

## 7. How to test Part 3

Run the API, the client and the seed script first (section 5), then open `http://localhost:5173/#/topology`.

### Test 10: the mesh loads
You should see 7 nodes (6 devices and the gateway) in a ring, green MST links, grey dashed unused links and a stats bar.
With the six seed devices it shows **13 wireless links**, **MST power cost 82**, **231 if every link was used** and
**65% power saved**.

### Test 11: route tracking by MAC address or identifier
In **Route Tracker** type `AA:BB:CC:00:11:05` (or `pump-a`) and press **Trace route**. The route is drawn in orange and
listed, with the MST route and the Dijkstra route and their costs. Type `nope` to see the error for an unknown device.

### Test 12: BFS, DFS and subnets
In **Connected Devices** switch between *Breadth-First* and *Depth-First* and choose *Zone 2 only*. The order and hop
counts change. With *Zone 2 only*, the list shows the Gateway (where the search starts) and just the Zone 2 devices.

### Test 13: failure and rerouting
Click `moisture-sensor-a`, then **Simulate failure**. The node turns red, its links turn dotted, and the report shows the
5 nodes that changed route (with their next hop) and the cost going from 82 to 78. Click **Recover node** to heal.

### Test 14: nodes cut off from the gateway
Fail `moisture-sensor-a`, then `power-meter-2`. **Connected Devices** now shows 4 nodes as *cut off from the gateway*, and
the MST cost falls to 0. Recover both nodes.

### Test 15: historical log (AVL range query)
Click `moisture-sensor-b`. In **Historical Telemetry Log** try *Last minute*, *Last hour* and *Custom range*. Each result
shows how many readings were found and how few tree nodes were touched. The same query without the UI:
```powershell
$from = (Get-Date).AddMinutes(-5).ToUniversalTime().ToString("o")
Invoke-RestMethod "http://localhost:5080/api/mesh/nodes/AA:BB:CC:00:11:02/history?from=$from&take=5"
```

### Test 16: AVL benchmark
In **AVL Tree Benchmark** choose 200,000 readings and press **Run benchmark**. The AVL query should be many times faster
than the list scan, and the tree is reported as balanced and correct.

### Test 17: editing links
In **Edit Links** choose `pump-a`, `GATEWAY` and cost `3`, press **Add link**: the MST power cost drops from 82 to 63
because a new cheap link exists. Choose the same two nodes and press **Remove link** to undo it.

### Test 18: all three pillars together
Move between **Sensor Ingestion**, **Command Stream** and **Network Topology**. The feed and device list keep their state.
Register a new device on the Ingestion page (for example zone `Zone 3`): it appears as a new node on the topology page.

---

## 8. Tests 1–9: Parts 1 and 2 (queues, dictionaries, undo, predictions)

All steps use the **Command Stream** page (top navigation) after running the seed script. The PowerShell
snippets send readings to the API exactly like a device would.

### Test 1 — Queue (FIFO) for normal telemetry
Watch **Queue Processing → Total processed** climb every couple of seconds. Every normal reading from the
simulated devices goes into the normal-lane `Queue<T>` and is processed in the order it arrived.
```powershell
Invoke-RestMethod http://localhost:5080/api/queues/stats
```

### Test 2 — Priority queue: simulate a high-priority alert
**Using the UI:** on `moisture-sensor-a` click **Simulate spike**. For about 16 seconds the device sends
critical moisture readings. Watch **Priority processed** increase, the card turn red, and the alert toast say
*"handled via priority lane"*.

**Sending it like a device would:**
```powershell
$body = @{ macAddress = "AA:BB:CC:00:11:01"; value = 4 } | ConvertTo-Json
Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/telemetry -ContentType "application/json" -Body $body
```
The response says `lane: priority`. Readings that are critical bypass the normal lane because
`TelemetryProcessor` always empties the priority lane first. A worse reading (a lower moisture value, or a
bigger power spike) gets a smaller priority number and is processed before a milder critical one.

### Test 3 — Dictionary (instant lookup by MAC)
```powershell
# Known MAC: accepted (HTTP 202)
Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/telemetry -ContentType "application/json" `
  -Body (@{ macAddress = "AA:BB:CC:00:11:03"; value = 900 } | ConvertTo-Json)

# Unknown MAC: rejected (HTTP 404)
Invoke-RestMethod -Method Post -Uri http://localhost:5080/api/telemetry -ContentType "application/json" `
  -Body (@{ macAddress = "FF:FF:FF:FF:FF:FF"; value = 900 } | ConvertTo-Json)
```
Registering the same MAC twice on the Sensor Ingestion page is also rejected, because the MAC is the dictionary key.

### Test 4 — HashSet (no duplicate alerts)
Send the critical reading from Test 2 twice. **Active Faults** lists `moisture-sensor-a` only **once**, and only
one alert toast appears. Then click **Unplug** on `pump-a`: after about 10 seconds it shows *Disconnected* with
a single fault. Click **Reconnect** and the fault clears.

### Test 5 — Stack (Undo)
Click **Turn ON** on `pump-a`, then **Turn ON** on `relay-b`. Under **Override History** the newest command is
at the top. Press **Undo last** and `relay-b` goes back OFF first, then `pump-a`. Try **Shut down all** and then
**Undo last**: both devices come back on, because the whole action was one command on the stack.

### Test 6 — SortedDictionary (history in time order)
Click any device card to draw its timeline. The API returns the points already sorted by timestamp:
```powershell
Invoke-RestMethod "http://localhost:5080/api/devices/AA:BB:CC:00:11:01/history?take=10"
```

### Test 7 — Search and filter
Type part of a name (for example `pump`) and press **Search**, or use the category and status drop-downs
(*In alert*, *Disconnected*, *Online*). The filtering runs on the API (`GET /api/devices?q=&category=&state=`).

### Test 8 — Predictive Action and Recommendation Engine
The engine learns "after an alert on device X, the operator usually does Y". It needs to see the pattern
**twice** before it suggests anything. Do this twice:

1. On `moisture-sensor-a` click **Simulate spike** and wait for the alert.
2. Click **Turn ON** on `pump-a` (the moisture recovers a few seconds later and the fault clears).
3. Click **Undo last** to turn the pump back off.

On the third spike, the **Predictive Engine** panel shows under **Suggested Actions**:
*Turn pump-a ON — After an alert on moisture-sensor-a, you did this 2 of 2 times.* with an **Apply** button.
**Automated Insights** also flags devices with an active fault, and devices that had three or more out-of-range
readings in their last ten, even before anyone searches for them.

### Test 9 — Early warning (the proactive part of the engine)
A sudden spike gives no warning, but a sensor that is slowly getting worse does. On `moisture-sensor-b` click
**Simulate drift**. Its value falls by about 2.5% on every report. After 6 readings the engine can see a steady trend
and **Automated Insights** shows, for example:
*Early warning: moisture-sensor-b is falling by about 2.5% per reading and could hit its warning limit in about 5 readings.*
About 10 seconds later the normal alert fires. Turning on a pump stops the drift. The engine only warns when the last
6 readings move in the same direction every time, so ordinary random noise does not trigger it.

---

---

## 9. Where each requirement lives

### Part 3 (final PoE)

| Rubric criterion | Where |
|---|---|
| Tree Structures and Historical Telemetry Queries (30) | `Services/AvlTree.cs`, `Services/TelemetryArchive.cs`, `GET /api/mesh/nodes/{id}/history`, `HistoryLog.jsx`, benchmark in `Services/ArchiveBenchmark.cs` |
| Graphs, Network Traversals and Minimum Spanning Trees (30) | `Services/MeshNetwork.cs`, `Services/MeshAlgorithms.cs` (BFS, DFS, Prim's, Dijkstra), `Services/MinHeap.cs`, `MeshGraphView.jsx` |
| Implementation Report: README and code explanations (10) | section 6 of this README |
| Project Completion Report (10), Technology Recommendations (5) and Rationale (5) | the PDF report (sections 3 and 4) |
| Iterative Feedback Incorporation (5) | `FEEDBACK-UPDATES.md` |
| Main Menu Integration and Architecture Health (5) | `NavBar.jsx`, `Landing.jsx`, `App.jsx`, `context/TelemetryContext.jsx` |
| GitHub and README | commit history on the `part3` branch, this README |

### Part 2 requirements (still in the application)

This follows the Part 2 marking rubric.

| Rubric criterion (marks) | What to look for | Where |
|---|---|---|
| Main Menu Navigation and Active Pillar State (30) | Menu shows all three pillars; Network Topology is visible but disabled; switching between the Part 1 ingestion page and the Part 2 command stream keeps the live feed, device list and connection; all Part 1 features still work (registration, recursive validation, file upload, live feed) | `NavBar.jsx`, `Landing.jsx`, `context/TelemetryContext.jsx` (sits above the router), `GET /api/pillars` |
| Advanced Collections: Stacks and Queues (15) | `Queue<T>` for incoming telemetry; priority queue so critical alerts bypass the normal lane; `Stack<T>` of override commands with Undo | `Services/TelemetryQueues.cs`, `Services/TelemetryProcessor.cs`, `Services/CommandService.cs` |
| Lookup Structures: Dictionaries and Key-Value Maps (15) | `Dictionary` keyed by MAC address as the device cache (O(1) lookup when a packet arrives); `SortedDictionary` keeps each device's history in time order for the timeline graph | `Services/DeviceRegistry.cs`, `Services/TelemetryLog.cs`, `TimelineChart.jsx` |
| Unique Collections: Sets (10) | `HashSet<T>` of active faults and disconnected nodes, so a fault is only ever alerted once | `Services/FaultTracker.cs` |
| Predictive Action and Suggestion Engine (30) | Learns what the operator does after an alert and suggests it next time; searches and commands are tracked; early warning for sensors trending towards a limit; insights flag faulty and unstable devices; shown as "Suggested Actions" and "Automated Insights" | `Services/ActionPatternEngine.cs`, `SuggestionsPanel.jsx` |
| Search and filter (part of the command stream page) | Text search plus category and alert-state filters, run on the API | `FilterBar.jsx`, `GET /api/devices` → `DeviceRegistry.Search` |
| Simulated data must hit the API like a device | Fake devices send real HTTP requests | `Services/DeviceSimulator.cs` → `POST /api/telemetry` |
| GitHub and README | Many small commits with clear messages; this README | repository history, `README.md` |
| Docker (suggested in the brief) | API and client containers, one compose file | `SmartX.Api/Dockerfile`, `smartx-client/Dockerfile`, `docker-compose.yml` |

## 10. Known limitations

- All data is in memory, so restarting the API resets devices, links and history.
- Mesh links are generated when a device registers. A real mesh would measure signal strength.
- The AVL tree supports insert and range queries but not deletion, so history is never removed.
- The MST minimises the cost of the whole network, so one node's MST route can cost more than its cheapest single path
  (the dashboard shows both).
- The predictive engine (Part 2) is a frequency count, not machine learning.

## 11. Updates made from lecturer feedback

See `FEEDBACK-UPDATES.md`.

## 12. AI use declaration

I used an AI assistant (Claude, made by Anthropic) while building this project. It helped me plan the architecture,
generate the Part 1, Part 2 and Part 3 source code, find and fix build and runtime errors, and draft this README and the
report. I ran the application, tested the features listed in sections 7 and 8, and reviewed the code before submitting it.
The data-structure choices and the way the parts fit together are explained in the code comments. Any errors that remain
are mine to fix.

## 13. Code attribution

The code in this repository is written for this project. The sources below are the documentation, textbooks and tutorials
the data structures and algorithms are based on. I used them to understand how each one works, and I did not copy code
blocks from them. Each source is also credited in a comment at the top of the file where the idea is used.

| Concept | Where used | Source |
|---|---|---|
| `Queue<T>` | `TelemetryQueues.cs` | Microsoft (n.d.a) |
| `PriorityQueue<TElement,TPriority>` | `TelemetryQueues.cs` | Microsoft (n.d.b); Abuhakmeh (n.d.) |
| `Stack<T>` (Push / Pop) | `CommandService.cs` | Microsoft (n.d.c); Microsoft (n.d.d) |
| `Dictionary<TKey,TValue>` | `DeviceRegistry.cs` | Microsoft (n.d.e) |
| `SortedDictionary<TKey,TValue>` | `TelemetryLog.cs` | Microsoft (n.d.f); GeeksforGeeks (n.d.d) |
| `HashSet<T>` | `FaultTracker.cs`, `ActionPatternEngine.cs` | Microsoft (n.d.g) |
| `BackgroundService` / hosted services | `TelemetryProcessor.cs`, `DeviceSimulator.cs` | Microsoft (n.d.h) |
| SignalR hub and React client | `TelemetryHub.cs`, `TelemetryContext.jsx` | Jovanović (2023); Jovanović (n.d.) |
| React Context for shared state | `TelemetryContext.jsx` | React (n.d.) |
| **AVL tree** (insert, rotations, balance factor) | `AvlTree.cs` | Adelson-Velsky and Landis (1962); GeeksforGeeks (n.d.a); Cormen et al. (2022) |
| **Binary min-heap** | `MinHeap.cs` | GeeksforGeeks (n.d.c); Cormen et al. (2022) |
| **Graph, BFS, DFS** | `MeshAlgorithms.cs`, `MeshNetwork.cs` | Cormen et al. (2022) |
| **Prim's minimum spanning tree** | `MeshAlgorithms.cs` | Prim (1957); GeeksforGeeks (n.d.b); Cormen et al. (2022) |
| **Dijkstra's shortest path** | `MeshAlgorithms.cs` | Dijkstra (1959); Cormen et al. (2022) |

## 14. References

Abuhakmeh, K. (n.d.) *Working with .NET 6 PriorityQueue*. Available at: https://khalidabuhakmeh.com/working-with-dotnet-six-priorityqueue (Accessed: 10 October 2026).

Adelson-Velsky, G.M. and Landis, E.M. (1962) 'An algorithm for the organization of information', *Doklady Akademii Nauk SSSR*, 146, pp. 263–266.

Cormen, T.H., Leiserson, C.E., Rivest, R.L. and Stein, C. (2022) *Introduction to Algorithms*. 4th edn. Cambridge, MA: MIT Press.

Dijkstra, E.W. (1959) 'A note on two problems in connexion with graphs', *Numerische Mathematik*, 1(1), pp. 269–271.

GeeksforGeeks (n.d.a) *AVL Tree | Set 1 (Insertion)*. Available at: https://www.geeksforgeeks.org/avl-tree-set-1-insertion/ (Accessed: 29 October 2026).

GeeksforGeeks (n.d.b) *Prim's algorithm using priority_queue in STL*. Available at: https://www.geeksforgeeks.org/prims-algorithm-using-priority_queue-stl/ (Accessed: 29 October 2026).

GeeksforGeeks (n.d.c) *Types of Heap Data Structure*. Available at: https://www.geeksforgeeks.org/types-of-heap-data-structure (Accessed: 29 October 2026).

GeeksforGeeks (n.d.d) *SortedDictionary Class in C#*. Available at: https://www.geeksforgeeks.org/sorteddictionary-class-in-c-sharp (Accessed: 10 October 2026).

Jovanović, M. (2023) *Building Real-Time Applications With SignalR & .NET 7* [YouTube video]. 4 July. Available at: https://www.youtube.com/watch?v=9_pRk7PwkpY (Accessed: 10 October 2026).

Jovanović, M. (n.d.) *Adding Real-Time Functionality To .NET Applications With SignalR*. Available at: https://milanjovanovic.tech/blog/adding-real-time-functionality-to-dotnet-applications-with-signalr (Accessed: 10 October 2026).

Microsoft (n.d.a) *Queue<T> Class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1 (Accessed: 10 October 2026).

Microsoft (n.d.b) *PriorityQueue<TElement,TPriority> Class*. Available at: https://learn.microsoft.com/dotnet/api/system.collections.generic.priorityqueue-2 (Accessed: 10 October 2026).

Microsoft (n.d.c) *Stack<T> Class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1 (Accessed: 10 October 2026).

Microsoft (n.d.d) *Stack<T>.Pop Method*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1.pop (Accessed: 10 October 2026).

Microsoft (n.d.e) *Dictionary<TKey,TValue> Class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2 (Accessed: 10 October 2026).

Microsoft (n.d.f) *SortedDictionary<TKey,TValue> Class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.sorteddictionary-2 (Accessed: 10 October 2026).

Microsoft (n.d.g) *HashSet<T> Class*. Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1 (Accessed: 10 October 2026).

Microsoft (n.d.h) *Background tasks with hosted services in ASP.NET Core*. Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services (Accessed: 10 October 2026).

Prim, R.C. (1957) 'Shortest connection networks and some generalizations', *Bell System Technical Journal*, 36(6), pp. 1389–1401.

React (n.d.) *Passing Data Deeply with Context*. Available at: https://react.dev/learn/passing-data-deeply-with-context (Accessed: 10 October 2026).

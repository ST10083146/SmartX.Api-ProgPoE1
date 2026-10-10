# Smart-X IoT Mesh Ecosystem — Part 2: Real-Time Processing and Device Management

PROG7312 / AAPD7112 — Portfolio of Evidence, Part 2.

Part 1 was a data ingestion gateway. Part 2 turns it into a real-time processing engine: telemetry
arrives at the API like it would from a real ESP32, waits in queues, is processed by a background worker,
and is shown on a live dashboard where an operator can send override commands, undo them, search
devices, and get suggested actions.

**Stack:** .NET 10 ASP.NET Core Minimal API (backend) + React with Vite (frontend) + SignalR (live updates).

---

## 1. How it works

```
 Simulated devices                      ASP.NET Core API                                   Browser
 (HTTP POST, like a real ESP32)
        |                                    |
        |  POST /api/telemetry               |
        +----------------------------------> |  Dictionary lookup by MAC (is this device registered?)
                                             |  Classify reading: Normal / Warning / Critical
                                             |        |                         |
                                             |   Queue<T> (normal)     PriorityQueue (critical)
                                             |        \                       /
                                             |      TelemetryProcessor (background worker)
                                             |        - always empties the priority lane first
                                             |        - updates device state (Dictionary)
                                             |        - stores history (SortedDictionary)
                                             |        - raises / clears faults (HashSet)
                                             |        - feeds the pattern engine
                                             |                    |
                                             |             SignalR hub  ------------------>  Live dashboard
                                             |                                              (device cards, feed,
   Operator clicks "Turn ON" / "Undo"  ----> |  Stack<T> of override commands                faults, suggestions)
```

The simulated devices are a background service (`DeviceSimulator`) that sends **real HTTP requests** to the
API's own `/api/telemetry` endpoint. Nothing is written into memory directly, so the API cannot tell them
apart from real hardware. Actuators (pump / relay) also ask the API `GET /api/devices/{mac}/desired-state`
to find out what state the operator wants, which is how a real relay would receive a command.

## 2. Project structure

```
SmartX/
├── docker-compose.yml
├── scripts/seed-demo-devices.ps1       registers six demo devices
├── SmartX.Api/                         .NET 10 backend
│   ├── Endpoints/                      TelemetryEndpoints, CommandEndpoints, SimulatorEndpoints
│   ├── Models/                         Part 1 models + DeviceModels, OverrideCommand, TelemetryMessage, Severity
│   ├── Services/
│   │   ├── TelemetryQueues.cs          Queue<T> + PriorityQueue
│   │   ├── DeviceRegistry.cs           Dictionary<string, DeviceState> keyed by MAC
│   │   ├── TelemetryLog.cs             SortedDictionary history per device
│   │   ├── FaultTracker.cs             HashSet of active faults
│   │   ├── CommandService.cs           Stack<T> of override commands (Undo)
│   │   ├── ActionPatternEngine.cs      predictive suggestions + insights
│   │   ├── TelemetryProcessor.cs       background worker that drains the queues
│   │   ├── DeviceSimulator.cs          fake devices that call the API over HTTP
│   │   └── (Part 1) SensorRegistry, TelemetryHistoryStore, AnomalyDetector
│   └── Program.cs
└── smartx-client/                      React dashboard
    └── src/
        ├── context/TelemetryContext.jsx   one shared SignalR connection + state (survives navigation)
        ├── pages/                         Landing, IngestionDashboard (Part 1), CommandStream (Part 2)
        └── components/                    NavBar, DeviceCard, FilterBar, QueueStats, TimelineChart,
                                           CommandHistory, FaultList, SuggestionsPanel, AlertToast, ...
```

## 3. Prerequisites

- .NET 10 SDK
- Node.js 18 or newer (npm comes with it)
- Docker Desktop (optional, only for the Docker option)

## 4. Running it

### Option A — manually (two terminals)

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

**Terminal 3 (optional), register demo devices:**
```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\seed-demo-devices.ps1
```
(`-ExecutionPolicy Bypass` is only needed if Windows says running scripts is disabled.)

The devices only start reporting about 4 seconds after the API starts, then every 2 seconds.

### Option B — Docker Compose (one command)

```powershell
docker compose up --build
```
Dashboard: `http://localhost:5173` — API: `http://localhost:5080`. Then run the seed script above.

---

## 5. How to test the queue and dictionary implementations

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

## 6. Where each Part 2 requirement lives

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

## 7. Known limitations

- All data is held in memory, so restarting the API resets devices, history, faults and learned patterns.
- The predictive engine is a simple frequency count, not machine learning. It only suggests an action after the
  same alert-then-action pattern has happened at least twice within a 3-minute window.
- Actuator "reported state" is simulated: the fake relay reports whatever state the API says it should be in.
- A spike in the simulator lasts about 16 seconds, and moisture spikes recover early if any actuator is ON.
- The early warning needs 6 readings and a steady trend. A sudden spike has no trend, so only the normal alert covers it.
- Network Topology and Mesh Routing is intentionally disabled; it belongs to the final PoE.

---

## 8. AI use declaration

I used an AI assistant (Claude, made by Anthropic) while building this project. It helped me plan the architecture,
generate the Part 1 and Part 2 source code, find and fix build and runtime errors, and draft this README. I ran the
application, tested the features listed in section 5, and reviewed the code before submitting it. The data-structure
choices and the way the parts fit together are explained in the code comments. Any errors that remain are mine to fix.

## 9. Code attribution

The code in this repository is written for this project. The sources below are the documentation and tutorials
the data structures and patterns are based on. I used them to understand how each class behaves and how it is meant to
be used, and I did not copy code blocks from them. Each source is also credited in a comment at the top of the
file where the idea is used.

| Concept | Where used | Source |
|---|---|---|
| `Queue<T>` | `TelemetryQueues.cs` | Microsoft (n.d.a) |
| `PriorityQueue<TElement,TPriority>` | `TelemetryQueues.cs` | Microsoft (n.d.b); Abuhakmeh (n.d.) |
| `Stack<T>` (Push / Pop) | `CommandService.cs` | Microsoft (n.d.c); Microsoft (n.d.d) |
| `Dictionary<TKey,TValue>` | `DeviceRegistry.cs` | Microsoft (n.d.e) |
| `SortedDictionary<TKey,TValue>` | `TelemetryLog.cs` | Microsoft (n.d.f); GeeksforGeeks (n.d.) |
| `HashSet<T>` | `FaultTracker.cs`, `ActionPatternEngine.cs` | Microsoft (n.d.g) |
| `BackgroundService` / hosted services | `TelemetryProcessor.cs`, `DeviceSimulator.cs` | Microsoft (n.d.h) |
| SignalR hub and React client | `TelemetryHub.cs`, `TelemetryContext.jsx` | Jovanović (2023); Jovanović (n.d.) |
| React Context for shared state | `TelemetryContext.jsx` | React (n.d.) |

## 10. References

Abuhakmeh, K. (n.d.) *Working with .NET 6 PriorityQueue*. Available at: https://khalidabuhakmeh.com/working-with-dotnet-six-priorityqueue (Accessed: 10 October 2026).

GeeksforGeeks (n.d.) *SortedDictionary Class in C#*. Available at: https://www.geeksforgeeks.org/sorteddictionary-class-in-c-sharp (Accessed: 10 October 2026).

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

React (n.d.) *Passing Data Deeply with Context*. Available at: https://react.dev/learn/passing-data-deeply-with-context (Accessed: 10 October 2026).

# Smart-X IoT Mesh Ecosystem — Part 1: Data Ingestion and Validation Gateway

PROG7312 / AAPD7112 — Portfolio of Evidence, Part 1 (Task 2: Implementation, 80 marks).

A simulated Smart-X gateway: a .NET 10 minimal API backend ingests and validates
telemetry from a mock ESP32 mesh, and a React dashboard registers sensors,
attaches config/log files, and shows a live telemetry feed with anomaly
alerts (the dynamic engagement feature).

## Architecture

**Web-First** combination: ASP.NET Core Minimal API backend + React (Vite) frontend,
talking over REST for commands and SignalR for the live feed.SmartX/
+-- SmartX.Api/ .NET 10 backend
| +-- Models/ TelemetryPacket<T>, PowerReading, DeploymentNode, Sensor
| +-- Services/ SensorRegistry, TelemetryHistoryStore, AnomalyDetector, TelemetrySimulator
| +-- Hubs/ TelemetryHub (SignalR)
| +-- Program.cs Minimal API endpoints
+-- smartx-client/ React dashboard (Vite)
+-- src/
+-- pages/ Landing (3 pillars), IngestionDashboard
+-- components/ SensorRegistrationForm, FileUploader, LiveTelemetryFeed
+-- api/client.js REST + SignalR client
## Prerequisites

- .NET 10 SDK
- Node.js 18+ and npm

## Running it

**Backend** (from `SmartX.Api/`):
```bash
dotnet restore
dotnet build
dotnet run
```
API listens on `http://localhost:5080`.

**Frontend** (from `smartx-client/`, in a second terminal):
```bash
npm install
npm run dev
```
Dashboard opens on `http://localhost:5173`.

Register a sensor first — the telemetry simulator only generates data for
sensors that exist, and the live feed panel will sit empty until you do.

## Where each rubric requirement lives

| Requirement | Where |
|---|---|
| Generics (no boxing) | `Models/TelemetryPacket.cs` — `TelemetryPacket<T> where T : struct` |
| Operator overloading | `Models/PowerReading.cs` — `+`, `-`, comparison operators; used live in `Services/TelemetrySimulator.cs` |
| Jagged arrays -> `List<T>` | `Services/TelemetryHistoryStore.cs` — `IngestBatch(float[][] rawBatch, ...)` flushed into `List<TelemetryPacket<float>>` |
| Recursion (nested deployment trees) | `Models/DeploymentNode.cs` — `DeploymentValidator.Validate` walks Facility -> Zone -> Sub-Zone -> Node recursively |
| Startup landing / 3 pillars | `GET /api/pillars` + `pages/Landing.jsx` |
| Sensor registration (MAC, location, category) | `POST /api/sensors` + `components/SensorRegistrationForm.jsx` |
| File/log/photo upload | `POST /api/sensors/{id}/upload` + `components/FileUploader.jsx` |
| Dynamic engagement feature | `Hubs/TelemetryHub.cs` + `components/LiveTelemetryFeed.jsx` — live feed with anomaly toasts, not a progress bar |
| Compiles/runs frontend + backend together | See "Running it" above |

## Notes on the simulation

Per the assignment's data-seeding note, this environment simulates the ESP32
mesh: `Services/TelemetrySimulator.cs` is a `BackgroundService` that generates
mock readings for every registered sensor every ~2 seconds, feeds them
through the jagged-array ingestion pipeline, and pushes them to the dashboard
over SignalR.

## Git

```bash
git init
git add .
git commit -m "feat: scaffold Smart-X ingestion gateway"
git branch -M main
git remote add origin https://github.com/ST10083146/SmartX.Api-ProgPoE1.git
git push -u origin main
```

Aim for 20+ small, meaningful commits rather than one giant one — commit as
each piece lands (models, then services, then endpoints, then each frontend
component) to build a real history, not just to hit the number.

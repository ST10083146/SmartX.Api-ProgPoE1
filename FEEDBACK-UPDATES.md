# Updates Made from Lecturer Feedback (Parts 1 and 2)

This file lists the changes I made to the Smart-X project because of the feedback I received on Part 1 and Part 2.
Each row links one piece of feedback to the change that answers it and to where that change lives in the project.

## Part 2 feedback (total score 42 / 100)

| # | Criterion and mark | Feedback received | Update made | Where |
|---|---|---|---|---|
| 1 | Main Menu Navigation and Active Pillar State: 8 / 30 | "No video demo provided" | I recorded a demo video that walks through the navigation between all three pillars and shows that the live feed and device list are kept when changing pages: PASTE-YOUTUBE-LINK-HERE. The Network Topology pillar is now active, so the menu has all three working pillars. README Test 18 explains how to check that state is kept. | `NavBar.jsx`, `Landing.jsx`, `App.jsx`, `context/TelemetryContext.jsx`; README section 7 (Test 18) |
| 2 | Predictive Action and Suggestion Engine: 8 / 30 | "No video provided to see this." | The engine only showed a suggestion after an alert and the same reaction had been repeated twice, so someone without a video could never see it. I added a **What the engine has learned** list that shows every pattern the engine has recorded and whether it is ready to suggest. I also added a **Load demo history** button that fills in two earlier alerts so a suggestion can be shown in seconds. It is labelled as demo data on screen and in the suggestion text. The engine is also shown in the video: PASTE-YOUTUBE-LINK-HERE. | `Services/ActionPatternEngine.cs` (`GetLearnedPatterns`, `SeedDemoHistory`), `Endpoints/TelemetryEndpoints.cs` (`GET /api/patterns`, `POST /api/patterns/demo`), `SuggestionsPanel.jsx`, `CommandStream.jsx`; README Test 8 |
| 3 | Advanced Collections: Stacks and Queues: 10 / 15 | No written comment. The band text names two possible faults: high-priority alerts not bypassing the standard stream, and the stack undo crashing. | No code change. I re-tested both named behaviours using README Tests 2 and 5. Critical readings go through the priority lane and are processed before the normal queue, and Undo and Shut down all reverse commands without errors. Both are shown in the video: PASTE-YOUTUBE-LINK-HERE. | `Services/TelemetryQueues.cs`, `Services/CommandService.cs`; README Tests 2 and 5 |
| 4 | Lookup Structures: Dictionaries and Key-Value Maps: 10 / 15 | No written comment. The band text names two possible faults: poor keys causing collisions, and timelines not sorting correctly. | No code change. I re-tested lookup by MAC address and the sorted history using README Tests 3 and 6. The MAC address is the dictionary key, and the history is returned in time order. | `Services/DeviceRegistry.cs`, `Services/TelemetryLog.cs`; README Tests 3 and 6 |
| 5 | Unique Collections: Sets: 6 / 10 | No written comment. The band text names two possible faults: sets used incorrectly, and duplicate warnings leaking through to the interface. | No code change. I re-tested duplicate handling using README Test 4. Sending the same critical reading twice produces one fault and one alert, and an unplugged device is reported once. | `Services/FaultTracker.cs`; README Test 4 |
| 6 | GitHub manual deduction | Not scored (no deduction). | None needed. | |

## Part 1 feedback

| # | Criterion and mark | Feedback received | Update made | Where |
|---|---|---|---|---|
| 1 | Part 1 overall: 70 / 100 | No video demo was provided, so the marker could not see the features working. | I recorded one demo video of the whole application that also shows the Part 1 features: sensor registration validated against the Facility, Zone, Sub-Zone and Node tree, file upload and the telemetry history. Video: PASTE-YOUTUBE-LINK-HERE | `Services/SensorRegistry.cs`, `Services/DeploymentValidator.cs`, the Sensor Ingestion page; README Part 1 section |

## Other improvements made in Part 3 (not from feedback)

These were found by testing the application and are listed for completeness:

| Improvement | Where |
|---|---|
| The mesh links are generated so devices connect to neighbours, not all straight to the gateway, so routes are multi-hop | `Services/MeshNetwork.cs` |
| The route panel shows Dijkstra's shortest path next to the MST route, because the MST minimises the whole network, not each path | `Services/MeshAlgorithms.cs`, `RoutePanel.jsx` |
| All three pillars live in one navigation bar, and the live connection and state sit above the router so nothing is lost when changing pages | `NavBar.jsx`, `context/TelemetryContext.jsx` |

# Updates Made from Lecturer Feedback (Parts 1 and 2)

This file lists the changes I made to the Smart-X project because of the feedback I received on Part 1 and Part 2.
Each row links one piece of feedback to the change that answers it and to where that change lives in the code.

<!--
HOW TO FILL THIS IN (delete this comment before submitting):
1. Open your marked Part 1 and Part 2 rubrics and copy each piece of feedback into the first column (quote it or paraphrase it).
2. In the second column say what you changed. Be specific and only list changes you really made.
3. In the third column give the files and, if you can, the commit message. Run `git log --oneline` to find the commit.
4. If a piece of feedback was not acted on, say so and why. An honest "not changed, because..." is better than a made-up change.
5. The rows marked EXAMPLE below only show the layout. Delete them.
-->

## Part 1 feedback

| # | Feedback received | Update made | Where (files / commits) |
|---|---|---|---|
| 1 | *EXAMPLE: "Referencing was incomplete."* | *EXAMPLE: Added IIE Harvard references to the README and a source comment at the top of each file.* | *EXAMPLE: README.md, Services/*.cs* |
| 2 | [paste feedback] | [what you changed] | [files / commit] |
| 3 | [paste feedback] | [what you changed] | [files / commit] |

## Part 2 feedback

| # | Feedback received | Update made | Where (files / commits) |
|---|---|---|---|
| 1 | [paste feedback] | [what you changed] | [files / commit] |
| 2 | [paste feedback] | [what you changed] | [files / commit] |
| 3 | [paste feedback] | [what you changed] | [files / commit] |

## Other improvements made in Part 3 (not from feedback)

These were found by testing the application and are listed for completeness:

| Improvement | Where |
|---|---|
| The mesh links are generated so devices connect to neighbours, not all straight to the gateway, so routes are multi-hop | `Services/MeshNetwork.cs` |
| The route panel shows Dijkstra's shortest path next to the MST route, because the MST minimises the whole network, not each path | `Services/MeshAlgorithms.cs`, `RoutePanel.jsx` |
| All three pillars live in one navigation bar, and the live connection and state sit above the router so nothing is lost when changing pages | `NavBar.jsx`, `context/TelemetryContext.jsx` |

# Commits the Part 3 work (Network Topology and Mesh Routing) as 26 small commits, in the order it was built.
# Run it from the ROOT of your cloned repo (the folder that contains SmartX.Api and smartx-client)
# AFTER you have created the part3 branch and copied the Part 3 files over it. DO NOT make an "initial commit"
# of the whole folder first, or this script will find nothing left to commit.
#
# The script remembers where it got to: every time you run it, it carries on with the steps that
# are not committed yet. So you can run it a few times across the day.
#
# Examples:
#   .\commit-part3-v1.0.ps1 -Max 4                       commit the next 4 steps, then stop
#   .\commit-part3-v1.0.ps1 -Max 4 -Push                 same, and push them to GitHub
#   .\commit-part3-v1.0.ps1 -WaitMinutes 105 -Push       commit one step, push, wait 1h45, repeat until done
#   .\commit-part3-v1.0.ps1 -Stage backend               only the backend steps (backend, frontend, docs)
#
# If Windows says running scripts is disabled, start it like this instead:
#   powershell -ExecutionPolicy Bypass -File .\commit-part3-v1.0.ps1 -Max 4 -Push
#
# Run it on the part3 branch. -Push pushes the current branch (part3) to origin.
# Commit dates are ALWAYS the real date and time of the moment each commit is made.
# This script never changes or back-dates them, because the history is meant to show when the work was committed.

param(
    [ValidateSet('all', 'backend', 'frontend', 'docs')]
    [string]$Stage = 'all',
    [int]$Max = 1000,
    [int]$WaitMinutes = 0,
    [switch]$Push
)

if (-not (Test-Path ".\SmartX.Api") -or -not (Test-Path ".\smartx-client")) {
    Write-Host "Run this from the repo root (the folder with SmartX.Api and smartx-client)." -ForegroundColor Red
    exit 1
}

git config user.name "ST10083146"
git config user.email "ST10083146@myemeris.edu.za"

$script:made = 0
$script:waiting = 0

function Finish-Commit {
    param([string]$Message)

    # With -WaitMinutes, pause before every commit except the first one of this run.
    if ($WaitMinutes -gt 0 -and $script:made -gt 0) {
        Write-Host "Waiting $WaitMinutes minutes before the next commit..." -ForegroundColor DarkGray
        Start-Sleep -Seconds ($WaitMinutes * 60)
    }

    git commit -m $Message --quiet
    if ($LASTEXITCODE -eq 0) {
        $script:made++
        Write-Host ("OK  [{0}] {1}" -f (Get-Date -Format "HH:mm"), $Message) -ForegroundColor Green
        if ($Push) { git push -u origin HEAD --quiet }
    }
    else {
        Write-Host "Commit failed for: $Message" -ForegroundColor Red
    }
}

function Save-Commit {
    param([string[]]$Paths, [string]$Message)

    if ($script:made -ge $Max) {
        if (git status --porcelain -- $Paths) { $script:waiting++ }
        return
    }

    git add -- $Paths
    if ($LASTEXITCODE -ne 0) {
        Write-Host "Could not stage: $Message" -ForegroundColor Red
        return
    }

    git diff --cached --quiet
    if ($LASTEXITCODE -eq 0) { return }   # nothing new here, so this step is already committed

    Finish-Commit $Message
}

# ---------- backend ----------
if ($Stage -eq 'all' -or $Stage -eq 'backend') {
    Save-Commit @("SmartX.Api/Services/AvlTree.cs") "feat: add self-balancing AVL tree with range queries"
    Save-Commit @("SmartX.Api/Services/MinHeap.cs") "feat: add hand-written binary min-heap"
    Save-Commit @("SmartX.Api/Services/TelemetryArchive.cs") "feat: add per-sensor AVL telemetry archive"
    Save-Commit @("SmartX.Api/Services/ArchiveBenchmark.cs") "feat: add AVL tree versus plain list benchmark"
    Save-Commit @("SmartX.Api/Services/MeshAlgorithms.cs") "feat: add graph algorithms (BFS, DFS, Prim's MST, Dijkstra)"
    Save-Commit @("SmartX.Api/Models/MeshModels.cs") "feat: add mesh network model records"
    Save-Commit @("SmartX.Api/Services/MeshNetwork.cs") "feat: add mesh network service with failure and rerouting"
    Save-Commit @("SmartX.Api/Endpoints/MeshEndpoints.cs") "feat: add mesh topology, route, history and benchmark endpoints"
    Save-Commit @("SmartX.Api/Services/TelemetryProcessor.cs") "feat: store every processed reading in the AVL archive"
    Save-Commit @("SmartX.Api/Program.cs") "feat: register mesh services and add new devices to the graph"
}

# ---------- frontend ----------
if ($Stage -eq 'all' -or $Stage -eq 'frontend') {
    Save-Commit @("smartx-client/src/api/client.js") "feat: extend API client for mesh endpoints"
    Save-Commit @("smartx-client/src/context/TelemetryContext.jsx") "feat: expose mesh change signal from the shared context"
    Save-Commit @("smartx-client/src/components/MeshGraphView.jsx") "feat: add SVG mesh graph view"
    Save-Commit @("smartx-client/src/components/RoutePanel.jsx") "feat: add route tracker with MST and Dijkstra routes"
    Save-Commit @("smartx-client/src/components/NodeControls.jsx") "feat: add node failure and recovery controls"
    Save-Commit @("smartx-client/src/components/HistoryLog.jsx") "feat: add historical telemetry log with time ranges"
    Save-Commit @("smartx-client/src/components/ConnectedPanel.jsx") "feat: add BFS and DFS connected devices panel"
    Save-Commit @("smartx-client/src/components/BenchmarkPanel.jsx") "feat: add AVL benchmark panel"
    Save-Commit @("smartx-client/src/components/LinkEditor.jsx") "feat: add link editor for the mesh graph"
    Save-Commit @("smartx-client/src/pages/NetworkTopology.jsx") "feat: add Network Topology page"
    Save-Commit @("smartx-client/src/components/NavBar.jsx") "feat: enable Network Topology in the navigation bar"
    Save-Commit @("smartx-client/src/pages/Landing.jsx") "feat: enable Network Topology pillar on the landing page"
    Save-Commit @("smartx-client/src/App.jsx") "feat: add Network Topology route to the app shell"
    Save-Commit @("smartx-client/src/styles/theme.css") "style: add Part 3 topology styles"
}

# ---------- docs ----------
if ($Stage -eq 'all' -or $Stage -eq 'docs') {
    Save-Commit @("README.md") "docs: update README for the final PoE with data structure explanations"
    Save-Commit @("FEEDBACK-UPDATES.md") "docs: add feedback updates ledger"
}

Write-Host ""
Write-Host "Commits made in this run: $script:made" -ForegroundColor Cyan
if ($script:waiting -gt 0) {
    Write-Host "Steps still waiting: $script:waiting. Run the script again later to continue." -ForegroundColor Yellow
}
Write-Host ("Total commits in this repo: " + (git rev-list --count HEAD)) -ForegroundColor Cyan

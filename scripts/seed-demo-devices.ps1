# Registers six demo devices so the Command Stream page has something to show.
# Run from anywhere while the API is running:   .\scripts\seed-demo-devices.ps1
param([string]$BaseUrl = "http://localhost:5080")

$devices = @(
    @{ macAddress = "AA:BB:CC:00:11:01"; zone = "Zone 1"; room = "Room 1"; nodeId = "moisture-sensor-a"; category = "Environmental" },
    @{ macAddress = "AA:BB:CC:00:11:02"; zone = "Zone 1"; room = "Room 2"; nodeId = "moisture-sensor-b"; category = "Environmental" },
    @{ macAddress = "AA:BB:CC:00:11:03"; zone = "Zone 1"; room = "Room 3"; nodeId = "power-meter-1";     category = "PowerConsumption" },
    @{ macAddress = "AA:BB:CC:00:11:04"; zone = "Zone 2"; room = "Room 1"; nodeId = "power-meter-2";     category = "PowerConsumption" },
    @{ macAddress = "AA:BB:CC:00:11:05"; zone = "Zone 2"; room = "Room 2"; nodeId = "pump-a";            category = "Actuator" },
    @{ macAddress = "AA:BB:CC:00:11:06"; zone = "Zone 2"; room = "Room 3"; nodeId = "relay-b";           category = "Actuator" }
)

foreach ($device in $devices) {
    try {
        Invoke-RestMethod -Method Post -Uri "$BaseUrl/api/sensors" -ContentType "application/json" -Body ($device | ConvertTo-Json) | Out-Null
        Write-Host "Registered $($device.nodeId)" -ForegroundColor Green
    }
    catch {
        Write-Host "Skipped $($device.nodeId): $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

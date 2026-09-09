using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using SmartX.Api.Hubs;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Simulates the ESP32 mesh publishing telemetry, since this environment is
/// a simulation per the brief's "Note on Hardware and Data Seeding". Every
/// tick it:
///   1. builds a small jagged batch of raw readings per sensor and flushes
///      it into the optimised List&lt;T&gt; history,
///   2. runs the anomaly check and pushes a live update over SignalR (the
///      dashboard's dynamic engagement feature — not a progress bar),
///   3. occasionally combines two power meters with the overloaded '+'
///      operator to demonstrate PowerReading aggregation live.
/// </summary>
public sealed class TelemetrySimulator : BackgroundService
{
    private readonly SensorRegistry _registry;
    private readonly TelemetryHistoryStore _history;
    private readonly AnomalyDetector _detector;
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly Random _rng = new();

    public TelemetrySimulator(
        SensorRegistry registry,
        TelemetryHistoryStore history,
        AnomalyDetector detector,
        IHubContext<TelemetryHub> hub)
    {
        _registry = registry;
        _history = history;
        _detector = detector;
        _hub = hub;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);

            var sensors = _registry.All;
            if (sensors.Count == 0) continue;

            foreach (var sensor in sensors)
            {
                var unit = sensor.Category switch
                {
                    SensorCategory.Environmental => "%",
                    SensorCategory.PowerConsumption => "W",
                    SensorCategory.Actuator => "",
                    _ => ""
                };

                // A genuinely jagged batch: 1-3 rows, each row 1-4 readings.
                var rowCount = _rng.Next(1, 4);
                var batch = new float[rowCount][];
                for (var row = 0; row < rowCount; row++)
                {
                    var colCount = _rng.Next(1, 5);
                    batch[row] = new float[colCount];
                    for (var col = 0; col < colCount; col++)
                    {
                        batch[row][col] = NextReading(sensor.Category);
                    }
                }

                _history.IngestBatch(sensor.Id, batch, unit, _detector, sensor.Category);

                var latest = batch[^1][^1];
                var anomalous = _detector.IsAnomalous(sensor.Category, latest);

                await _hub.Clients.All.SendAsync("telemetryTick", new
                {
                    sensorId = sensor.Id,
                    nodeId = sensor.NodeId,
                    zone = sensor.Zone,
                    category = sensor.Category.ToString(),
                    value = latest,
                    unit,
                    anomalous,
                    timestamp = DateTimeOffset.UtcNow
                }, stoppingToken);

                if (anomalous)
                {
                    await _hub.Clients.All.SendAsync("anomalyAlert", new
                    {
                        sensorId = sensor.Id,
                        nodeId = sensor.NodeId,
                        value = latest,
                        unit,
                        message = $"{sensor.NodeId} reported {latest:F1}{unit} — outside expected range."
                    }, stoppingToken);
                }
            }

            // Demonstrate operator-overloaded aggregation on two power meters, when available.
            var powerSensors = sensors.Where(s => s.Category == SensorCategory.PowerConsumption).ToList();
            if (powerSensors.Count >= 2)
            {
                var meter1 = new PowerReading(NextReading(SensorCategory.PowerConsumption), powerSensors[0].NodeId);
                var meter2 = new PowerReading(NextReading(SensorCategory.PowerConsumption), powerSensors[1].NodeId);
                var combined = meter1 + meter2; // operator overload in action: Meter3 = Meter1 + Meter2

                await _hub.Clients.All.SendAsync("meterAggregate", new
                {
                    combined.Watts,
                    label = combined.NodeId
                }, stoppingToken);
            }
        }
    }

    private float NextReading(SensorCategory category) => category switch
    {
        SensorCategory.Environmental => (float)(_rng.NextDouble() * 100),
        SensorCategory.PowerConsumption => (float)(_rng.NextDouble() * 3200),
        SensorCategory.Actuator => _rng.Next(0, 2),
        _ => 0f
    };
}

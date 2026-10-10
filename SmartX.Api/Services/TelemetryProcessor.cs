using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Takes telemetry out of the queues and does the real work:
/// updates the device (Dictionary), stores the reading in the sorted history,
/// raises or clears faults (HashSet), and pushes live updates to the dashboard over SignalR.
///
/// It always empties the priority lane before touching the normal lane, which is what makes
/// a critical alert "bypass the standard queue".
/// <para>References:</para>
///   Microsoft (n.d.h) Background tasks with hosted services in ASP.NET Core.
///     Available at: https://learn.microsoft.com/en-us/aspnet/core/fundamentals/host/hosted-services (Accessed: 10 October 2026).
/// </summary>
public class TelemetryProcessor : BackgroundService
{
    private readonly TelemetryQueues _queues;
    private readonly DeviceRegistry _devices;
    private readonly TelemetryLog _log;
    private readonly TelemetryHistoryStore _history;   // the Part 1 jagged-array pipeline, reused
    private readonly FaultTracker _faults;
    private readonly AnomalyDetector _detector;
    private readonly ActionPatternEngine _patterns;
    private readonly IHubContext<TelemetryHub> _hub;
    private readonly IConfiguration _config;
    private readonly ILogger<TelemetryProcessor> _logger;

    public TelemetryProcessor(
        TelemetryQueues queues,
        DeviceRegistry devices,
        TelemetryLog log,
        TelemetryHistoryStore history,
        FaultTracker faults,
        AnomalyDetector detector,
        ActionPatternEngine patterns,
        IHubContext<TelemetryHub> hub,
        IConfiguration config,
        ILogger<TelemetryProcessor> logger)
    {
        _queues = queues;
        _devices = devices;
        _log = log;
        _history = history;
        _faults = faults;
        _detector = detector;
        _patterns = patterns;
        _hub = hub;
        _config = config;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var disconnectAfter = TimeSpan.FromSeconds(_config.GetValue<int>("Telemetry:DisconnectAfterSeconds", 10));
        var lastWatchdogRun = DateTimeOffset.UtcNow;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var handled = 0;

                // Process in small batches so one huge burst can't starve the watchdog below.
                while (handled < 200 && _queues.TryDequeueNext(out var message, out var fromPriorityLane))
                {
                    if (message != null)
                    {
                        await ProcessAsync(message, fromPriorityLane, stoppingToken);
                    }
                    handled++;
                }

                if (handled > 0)
                {
                    await _hub.Clients.All.SendAsync("queueStats", _queues.GetStats(), stoppingToken);
                }

                if (DateTimeOffset.UtcNow - lastWatchdogRun > TimeSpan.FromSeconds(1))
                {
                    lastWatchdogRun = DateTimeOffset.UtcNow;
                    await CheckForSilentDevicesAsync(disconnectAfter, stoppingToken);
                }

                await Task.Delay(250, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                // One bad packet should never kill the whole processor.
                _logger.LogError(ex, "Telemetry processor hit an error and will keep going.");
            }
        }
    }

    private async Task ProcessAsync(TelemetryMessage message, bool fromPriorityLane, CancellationToken token)
    {
        var (info, wasDisconnected) = _devices.ApplyReading(message.MacAddress, message.Value, message.Severity, message.ReceivedAt);
        if (info == null)
        {
            return;
        }

        Enum.TryParse<SensorCategory>(info.Category, out var category);
        var anomalous = message.Severity != Severity.Normal;

        // Save the reading in the time-ordered log.
        var packet = new TelemetryPacket<float>(info.SensorId, message.Value, info.Unit)
        {
            Timestamp = message.ReceivedAt,
            IsAnomalous = anomalous
        };
        _log.Add(info.Mac, packet);

        // Part 1's jagged-array history, fed with a one-row batch.
        _history.IngestBatch(info.SensorId, new float[][] { new float[] { message.Value } }, info.Unit, _detector, category);

        // ---- faults (HashSet) ----
        if (wasDisconnected && _faults.Clear(info.Mac, "Disconnected"))
        {
            await _hub.Clients.All.SendAsync("faultCleared",
                new { mac = info.Mac, nodeId = info.NodeId, type = "Disconnected" }, token);
        }

        if (anomalous)
        {
            // Raise() is false if this fault is already active, so no duplicate alert is sent.
            if (_faults.Raise(info.Mac, "OutOfRange"))
            {
                _patterns.RecordAlert(info.Mac);

                await _hub.Clients.All.SendAsync("faultRaised",
                    new { mac = info.Mac, nodeId = info.NodeId, type = "OutOfRange" }, token);

                await _hub.Clients.All.SendAsync("anomalyAlert", new
                {
                    sensorId = info.SensorId,
                    nodeId = info.NodeId,
                    value = message.Value,
                    unit = info.Unit,
                    severity = message.Severity.ToString(),
                    viaPriorityLane = fromPriorityLane,
                    message = info.NodeId + " reported " + message.Value.ToString("F1") + info.Unit +
                              " (" + message.Severity + ")" + (fromPriorityLane ? " - handled via priority lane" : "")
                }, token);
            }
        }
        else if (_faults.Clear(info.Mac, "OutOfRange"))
        {
            await _hub.Clients.All.SendAsync("faultCleared",
                new { mac = info.Mac, nodeId = info.NodeId, type = "OutOfRange" }, token);
        }

        // ---- live updates for the dashboard ----
        await _hub.Clients.All.SendAsync("telemetryTick", new
        {
            sensorId = info.SensorId,
            mac = info.Mac,
            nodeId = info.NodeId,
            zone = info.Zone,
            category = info.Category,
            value = message.Value,
            unit = info.Unit,
            anomalous,
            severity = message.Severity.ToString(),
            lane = fromPriorityLane ? "priority" : "normal",
            timestamp = message.ReceivedAt
        }, token);

        await _hub.Clients.All.SendAsync("deviceUpdate", info, token);

        // Operator overloading from Part 1: add all the power meters together with "+".
        if (category == SensorCategory.PowerConsumption)
        {
            var meters = _devices.GetLatestPowerReadings();
            if (meters.Count >= 2)
            {
                var total = new PowerReading(0, "total");
                foreach (var meter in meters)
                {
                    total = total + meter;
                }
                await _hub.Clients.All.SendAsync("meterAggregate",
                    new { watts = total.Watts, label = meters.Count + " meters" }, token);
            }
        }
    }

    private async Task CheckForSilentDevicesAsync(TimeSpan timeout, CancellationToken token)
    {
        foreach (var device in _devices.MarkSilentDevices(timeout))
        {
            await _hub.Clients.All.SendAsync("deviceUpdate", device, token);

            // HashSet makes sure we only alert ONCE per disconnected sensor.
            if (_faults.Raise(device.Mac, "Disconnected"))
            {
                _patterns.RecordAlert(device.Mac);

                await _hub.Clients.All.SendAsync("faultRaised",
                    new { mac = device.Mac, nodeId = device.NodeId, type = "Disconnected" }, token);

                await _hub.Clients.All.SendAsync("anomalyAlert", new
                {
                    sensorId = device.SensorId,
                    nodeId = device.NodeId,
                    value = 0f,
                    unit = device.Unit,
                    severity = "Critical",
                    viaPriorityLane = false,
                    message = device.NodeId + " has stopped reporting (disconnected)."
                }, token);
            }
        }
    }
}

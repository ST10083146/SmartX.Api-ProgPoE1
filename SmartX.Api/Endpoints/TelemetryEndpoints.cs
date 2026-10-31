using SmartX.Api.Models;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class TelemetryEndpoints
{
    public static void MapTelemetryEndpoints(this WebApplication app)
    {
        // This is the endpoint a device (real or simulated) calls. It does NOT process anything.
        // It only checks the device exists and drops the reading in the right queue lane.
        app.MapPost("/api/telemetry", (TelemetryRequest request, DeviceRegistry devices, TelemetryQueues queues, AnomalyDetector detector) =>
        {
            var mac = DeviceRegistry.NormaliseMac(request.MacAddress);

            // Dictionary lookup by MAC: O(1), no looping through all devices.
            if (!devices.TryGet(mac, out var device) || device == null)
            {
                return Results.NotFound(new { error = "Unknown device. Register the MAC address first." });
            }

            Enum.TryParse<SensorCategory>(device.Category, out var category);
            var severity = detector.Classify(category, request.Value);

            var message = new TelemetryMessage
            {
                MacAddress = mac,
                Value = request.Value,
                Severity = severity,
                ReceivedAt = DateTimeOffset.UtcNow
            };

            // Worse readings get a smaller number, so they come out of the priority queue first.
            if (severity == Severity.Critical)
            {
                var deviation = Math.Min(100f, detector.Deviation(category, request.Value));
                message.Priority = 100 - (int)deviation;
            }

            var lane = queues.Enqueue(message);

            return Results.Accepted(value: new { lane, severity = severity.ToString() });
        });

        app.MapGet("/api/queues/stats", (TelemetryQueues queues) => Results.Ok(queues.GetStats()));

        // Search and filter. Only a submitted text search (log=true) is remembered by the pattern engine.
        app.MapGet("/api/devices", (string? q, string? category, string? state, bool? log, DeviceRegistry devices, ActionPatternEngine patterns) =>
        {
            if (log == true && !string.IsNullOrWhiteSpace(q))
            {
                patterns.RecordUserAction("search|" + q.Trim().ToLowerInvariant());
            }

            return Results.Ok(devices.Search(q, category, state));
        });

        // Sorted history for the timeline graph.
        app.MapGet("/api/devices/{mac}/history", (string mac, int? take, DeviceRegistry devices, TelemetryLog log) =>
        {
            var normalised = DeviceRegistry.NormaliseMac(mac);
            if (!devices.Contains(normalised))
            {
                return Results.NotFound();
            }

            var count = Math.Clamp(take ?? 60, 1, 300);
            var points = log.GetLatest(normalised, count)
                .Select(p => new LogPoint(p.Timestamp, p.Value, p.IsAnomalous))
                .ToList();

            return Results.Ok(points);
        });

        // Devices "poll" this to find out what state the operator wants (how a real relay would get a command).
        app.MapGet("/api/devices/{mac}/desired-state", (string mac, DeviceRegistry devices) =>
        {
            if (!devices.TryGetCommandedState(mac, out var on))
            {
                return Results.NotFound();
            }
            return Results.Ok(new { on });
        });

        app.MapGet("/api/faults", (FaultTracker faults, DeviceRegistry devices) =>
        {
            var list = new List<FaultInfo>();
            foreach (var fault in faults.GetAll())
            {
                devices.TryGet(fault.Mac, out var device);
                list.Add(new FaultInfo(fault.Mac, device != null ? device.NodeId : fault.Mac, fault.Type));
            }
            return Results.Ok(list);
        });

        app.MapGet("/api/suggestions", (ActionPatternEngine patterns) =>
            Results.Ok(new { suggestions = patterns.GetSuggestions(), insights = patterns.GetInsights() }));
    }
}

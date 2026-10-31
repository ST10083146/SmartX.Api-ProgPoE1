using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

/// <summary>
/// Buttons on the dashboard use these to make a simulated device misbehave, so alerts can be
/// demonstrated on demand. The device then still reports through the normal /api/telemetry endpoint.
/// </summary>
public static class SimulatorEndpoints
{
    public static void MapSimulatorEndpoints(this WebApplication app)
    {
        app.MapPost("/api/simulator/spike/{mac}", (string mac, DeviceRegistry devices, SimulatorControls controls) =>
        {
            var normalised = DeviceRegistry.NormaliseMac(mac);
            if (!devices.TryGet(normalised, out var device) || device == null)
            {
                return Results.NotFound();
            }
            if (device.Category == "Actuator")
            {
                return Results.BadRequest(new { error = "Actuators report on/off, so a spike makes no sense for them." });
            }

            controls.RequestSpike(normalised);
            return Results.Ok(new { message = device.NodeId + " will send critical readings for the next ~16 seconds." });
        });

        app.MapPost("/api/simulator/drift/{mac}", (string mac, DeviceRegistry devices, SimulatorControls controls) =>
        {
            var normalised = DeviceRegistry.NormaliseMac(mac);
            if (!devices.TryGet(normalised, out var device) || device == null)
            {
                return Results.NotFound();
            }
            if (device.Category == "Actuator")
            {
                return Results.BadRequest(new { error = "Actuators report on/off, so they cannot drift." });
            }

            controls.StartDrift(normalised);
            return Results.Ok(new { message = device.NodeId + " is now getting steadily worse. Watch for an early warning before the alert fires." });
        });

        app.MapPost("/api/simulator/disconnect/{mac}", (string mac, DeviceRegistry devices, SimulatorControls controls) =>
        {
            var normalised = DeviceRegistry.NormaliseMac(mac);
            if (!devices.TryGet(normalised, out var device) || device == null)
            {
                return Results.NotFound();
            }

            controls.Silence(normalised);
            return Results.Ok(new { message = device.NodeId + " is now unplugged. The watchdog will notice shortly." });
        });

        app.MapPost("/api/simulator/reconnect/{mac}", (string mac, DeviceRegistry devices, SimulatorControls controls) =>
        {
            var normalised = DeviceRegistry.NormaliseMac(mac);
            if (!devices.TryGet(normalised, out var device) || device == null)
            {
                return Results.NotFound();
            }

            controls.Resume(normalised);
            return Results.Ok(new { message = device.NodeId + " is plugged back in." });
        });
    }
}

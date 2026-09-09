using Microsoft.AspNetCore.SignalR;

namespace SmartX.Api.Hubs;

/// <summary>
/// Thin hub — the simulator pushes to clients via IHubContext, clients don't
/// call server methods here, they just subscribe to the "telemetryTick" and
/// "anomalyAlert" events. Kept separate from the simulator so the push
/// mechanism can be swapped (e.g. for a real MQTT bridge later) without
/// touching client code.
/// </summary>
public sealed class TelemetryHub : Hub
{
}

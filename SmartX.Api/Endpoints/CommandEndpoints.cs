using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Api.Models;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

public static class CommandEndpoints
{
    public static void MapCommandEndpoints(this WebApplication app)
    {
        app.MapPost("/api/commands", async (CommandRequest request, CommandService commands, DeviceRegistry devices,
            ActionPatternEngine patterns, IHubContext<TelemetryHub> hub) =>
        {
            bool turnOn;
            if (request.Action.Equals("On", StringComparison.OrdinalIgnoreCase)) turnOn = true;
            else if (request.Action.Equals("Off", StringComparison.OrdinalIgnoreCase)) turnOn = false;
            else return Results.BadRequest(new { error = "Action must be 'On' or 'Off'." });

            var mac = DeviceRegistry.NormaliseMac(request.MacAddress);
            var (ok, message, command) = commands.Execute(mac, turnOn);
            if (!ok)
            {
                return Results.BadRequest(new { error = message });
            }

            patterns.RecordUserAction("cmd|" + mac + "|" + (turnOn ? "on" : "off"));

            if (devices.TryGet(mac, out var info) && info != null)
            {
                await hub.Clients.All.SendAsync("deviceUpdate", info);
            }
            await hub.Clients.All.SendAsync("commandsChanged", new { message });

            return Results.Ok(new { message, command });
        });

        app.MapPost("/api/commands/shutdown-all", async (CommandService commands, DeviceRegistry devices,
            ActionPatternEngine patterns, IHubContext<TelemetryHub> hub) =>
        {
            var (ok, message, command) = commands.ShutDownAll();
            if (!ok)
            {
                return Results.BadRequest(new { error = message });
            }

            patterns.RecordUserAction("cmd|ALL|off");

            if (command != null)
            {
                foreach (var mac in command.PreviousStates.Keys)
                {
                    if (devices.TryGet(mac, out var info) && info != null)
                    {
                        await hub.Clients.All.SendAsync("deviceUpdate", info);
                    }
                }
            }
            await hub.Clients.All.SendAsync("commandsChanged", new { message });

            return Results.Ok(new { message, command });
        });

        app.MapPost("/api/commands/undo", async (CommandService commands, IHubContext<TelemetryHub> hub) =>
        {
            var (ok, message, changed) = commands.Undo();
            if (!ok)
            {
                return Results.BadRequest(new { error = message });
            }

            foreach (var info in changed)
            {
                await hub.Clients.All.SendAsync("deviceUpdate", info);
            }
            await hub.Clients.All.SendAsync("commandsChanged", new { message });

            return Results.Ok(new { message });
        });

        app.MapGet("/api/commands/history", (CommandService commands) => Results.Ok(commands.GetHistory()));
    }
}

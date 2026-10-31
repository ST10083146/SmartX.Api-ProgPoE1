using Microsoft.AspNetCore.SignalR;
using SmartX.Api.Hubs;
using SmartX.Api.Models;
using SmartX.Api.Services;

namespace SmartX.Api.Endpoints;

/// <summary>Part 3: Network Topology and Mesh Routing.</summary>
public static class MeshEndpoints
{
    public static void MapMeshEndpoints(this WebApplication app)
    {
        // the whole graph: nodes, links, which links are in the MST, who is reachable / down
        app.MapGet("/api/mesh/topology", (MeshNetwork mesh) => Results.Ok(mesh.GetTopology()));

        // route a node's telemetry takes to the gateway (accepts a MAC address OR a node id like "pump-a")
        app.MapGet("/api/mesh/route/{id}", (string id, MeshNetwork mesh) => Results.Ok(mesh.GetRoute(id)));

        // BFS / DFS: which devices are currently connected. Optional zone ("subnet") filter.
        app.MapGet("/api/mesh/connected", (string? from, string? zone, string? mode, MeshNetwork mesh) =>
            Results.Ok(mesh.GetConnected(from, zone, mode ?? "bfs")));

        app.MapPost("/api/mesh/links", async (LinkRequest request, MeshNetwork mesh, IHubContext<TelemetryHub> hub) =>
        {
            var error = mesh.AddLink(request.A, request.B, request.Cost);
            if (error != null)
            {
                return Results.BadRequest(new { error });
            }

            await hub.Clients.All.SendAsync("meshChanged", new { reason = "link added" });
            return Results.Ok(new { message = "Link added." });
        });

        app.MapDelete("/api/mesh/links", async (string a, string b, MeshNetwork mesh, IHubContext<TelemetryHub> hub) =>
        {
            var error = mesh.RemoveLink(a, b);
            if (error != null)
            {
                return Results.BadRequest(new { error });
            }

            await hub.Clients.All.SendAsync("meshChanged", new { reason = "link removed" });
            return Results.Ok(new { message = "Link removed." });
        });

        app.MapPost("/api/mesh/nodes/{id}/fail", async (string id, MeshNetwork mesh, IHubContext<TelemetryHub> hub) =>
        {
            var report = mesh.Fail(id);
            await hub.Clients.All.SendAsync("meshChanged", new { reason = "node failed" });
            return Results.Ok(report);
        });

        app.MapPost("/api/mesh/nodes/{id}/recover", async (string id, MeshNetwork mesh, IHubContext<TelemetryHub> hub) =>
        {
            var report = mesh.Recover(id);
            await hub.Clients.All.SendAsync("meshChanged", new { reason = "node recovered" });
            return Results.Ok(report);
        });

        // historical log for one sensor, answered with an AVL tree range query
        app.MapGet("/api/mesh/nodes/{id}/history", (string id, DateTimeOffset? from, DateTimeOffset? to, int? take,
            MeshNetwork mesh, DeviceRegistry devices, TelemetryArchive archive) =>
        {
            var resolved = mesh.Resolve(id);
            if (resolved == null || resolved == MeshNetwork.GatewayId || !devices.TryGet(resolved, out var device) || device == null)
            {
                return Results.NotFound(new { error = "No device with that MAC address or node id." });
            }

            var low = from ?? DateTimeOffset.MinValue;
            var high = to ?? DateTimeOffset.UtcNow.AddMinutes(5);

            var result = archive.Query(resolved, low, high);

            // newest first for the log, and only return as many rows as the page asked for
            var limit = Math.Clamp(take ?? 100, 1, 1000);
            var rows = result.Readings
                .OrderByDescending(r => r.Timestamp)
                .Take(limit)
                .Select(r => new HistoryPoint(r.Timestamp, r.Value, r.IsAnomalous))
                .ToList();

            return Results.Ok(new HistoryResult(
                resolved,
                device.NodeId,
                device.Unit,
                result.Readings.Count,
                rows.Count,
                result.NodesVisited,
                result.TreeSize,
                result.TreeHeight,
                Math.Round(result.Microseconds, 1),
                rows));
        });

        // AVL tree vs plain list on a big fake history
        app.MapGet("/api/mesh/benchmark", (int? size) => Results.Ok(ArchiveBenchmark.Run(size ?? 200_000)));
    }
}

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Holds every registered sensor plus the deployment tree
/// (Facility -> Zone -> Sub-Zone -> Node) those sensors belong to.
/// Every registration re-validates the tree recursively via
/// <see cref="DeploymentValidator"/> before the sensor is accepted —
/// that's what stops a malformed Room/Zone/Node combination from ever
/// reaching storage.
/// </summary>
public sealed class SensorRegistry
{
    private readonly ConcurrentDictionary<Guid, Sensor> _sensors = new();
    private readonly DeploymentNode _root = new()
    {
        Id = "root",
        Name = "Smart-X Facility",
        Tier = DeploymentTier.Facility
    };

    public IReadOnlyCollection<Sensor> All => _sensors.Values.ToList();

    public DeploymentNode Tree => _root;

    public (bool ok, Sensor? sensor, List<string> errors) Register(SensorRegistrationRequest request)
    {
        var zoneNode = FindOrCreate(_root, request.Zone, DeploymentTier.Zone);
        var subZoneNode = FindOrCreate(zoneNode, $"{request.Zone}/{request.Room}", DeploymentTier.SubZone);
        _ = FindOrCreate(subZoneNode, request.NodeId, DeploymentTier.Node);

        if (!DeploymentValidator.Validate(_root, out var errors))
        {
            return (false, null, errors);
        }

        var sensor = new Sensor
        {
            MacAddress = request.MacAddress,
            Room = request.Room,
            Zone = request.Zone,
            NodeId = request.NodeId,
            Category = request.Category
        };

        _sensors[sensor.Id] = sensor;
        return (true, sensor, errors);
    }

    public Sensor? Get(Guid id) => _sensors.TryGetValue(id, out var sensor) ? sensor : null;

    private static DeploymentNode FindOrCreate(DeploymentNode parent, string name, DeploymentTier tier)
    {
        var existing = parent.Children.FirstOrDefault(c => c.Name == name && c.Tier == tier);
        if (existing is not null) return existing;

        var created = new DeploymentNode
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = name,
            Tier = tier
        };
        parent.Children.Add(created);
        return created;
    }
}

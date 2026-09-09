using System;
using System.Collections.Generic;

namespace SmartX.Api.Models;

public sealed class Sensor
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string MacAddress { get; set; } = string.Empty;
    public string Room { get; set; } = string.Empty;
    public string Zone { get; set; } = string.Empty;
    public string NodeId { get; set; } = string.Empty;
    public SensorCategory Category { get; set; }
    public DateTimeOffset RegisteredAt { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>Paths (relative to wwwroot/uploads) of attached config files, deployment photos, or logs.</summary>
    public List<string> AttachedFiles { get; init; } = new();
}

/// <summary>DTO used for POST /api/sensors — kept separate from the domain model
/// so the wire contract can change independently of internal representation.</summary>
public sealed record SensorRegistrationRequest(
    string MacAddress,
    string Room,
    string Zone,
    string NodeId,
    SensorCategory Category);

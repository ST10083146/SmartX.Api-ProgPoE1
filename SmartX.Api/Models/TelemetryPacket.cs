using System;

namespace SmartX.Api.Models;

/// <summary>
/// Generic wrapper for a single telemetry reading published by a Smart-X node.
///
/// Why generics instead of "object Value": if we stored every reading as
/// `object`, every float/int/bool coming off an ESP32 would be boxed onto the
/// heap the moment it was assigned, and unboxed again every time it was read
/// back. On a gateway processing thousands of readings a second from a mesh
/// of edge devices, that's a lot of pointless heap churn and GC pressure.
///
/// TelemetryPacket&lt;T&gt; is a closed generic type per value kind
/// (TelemetryPacket&lt;float&gt;, TelemetryPacket&lt;int&gt;,
/// TelemetryPacket&lt;bool&gt;...). The JIT generates a specialised value-type
/// layout for each of those, so Value is stored inline — no boxing, no
/// casting, and the compiler enforces the type at the call site instead of at
/// runtime.
/// </summary>
/// <typeparam name="T">
/// The underlying telemetry value type. Constrained to struct so the whole
/// packet stays boxing-free — this is exactly the "avoid expensive
/// boxing/unboxing" requirement from the brief.
/// </typeparam>
public sealed class TelemetryPacket<T> where T : struct
{
    public Guid SensorId { get; init; }
    public T Value { get; init; }
    public string Unit { get; init; } = string.Empty;
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;

    /// <summary>
    /// True when this reading falls outside the sensor's expected band.
    /// Set by <see cref="Services.AnomalyDetector"/> at ingestion time and
    /// used by the dashboard's live engagement feature to flag spikes.
    /// </summary>
    public bool IsAnomalous { get; set; }

    public TelemetryPacket() { }

    public TelemetryPacket(Guid sensorId, T value, string unit)
    {
        SensorId = sensorId;
        Value = value;
        Unit = unit;
        Timestamp = DateTimeOffset.UtcNow;
    }

    public override string ToString() =>
        $"[{Timestamp:HH:mm:ss}] {SensorId}: {Value}{Unit}{(IsAnomalous ? " (!)" : string.Empty)}";
}

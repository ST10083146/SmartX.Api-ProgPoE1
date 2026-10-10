namespace SmartX.Api.Models;

/// <summary>
/// The live state of one registered device. Stored as the value in the
/// Dictionary in DeviceRegistry (the MAC address is the key).
/// </summary>
public class DeviceState
{
    public Sensor Sensor { get; set; } = new Sensor();
    public float? LastValue { get; set; }
    public string Unit { get; set; } = string.Empty;
    public Severity LastSeverity { get; set; } = Severity.Normal;
    public DeviceStatus Status { get; set; } = DeviceStatus.Online;

    // Last time the device sent anything (starts as the registration time).
    public DateTimeOffset LastContact { get; set; } = DateTimeOffset.UtcNow;

    // Only used when the sensor category is Actuator (pump / relay).
    public bool CommandedOn { get; set; }
}

/// <summary>
/// A read-only copy of a DeviceState that is safe to send to the frontend.
/// I send copies instead of the real objects so other threads can't change
/// them while they are being serialised.
/// </summary>
public record DeviceInfo(
    string Mac,
    string NodeId,
    string Zone,
    string Room,
    string Category,
    string Status,
    string Severity,
    float? LastValue,
    string Unit,
    DateTimeOffset LastContact,
    bool CommandedOn,
    Guid SensorId);

public record FaultInfo(string Mac, string NodeId, string Type);

public record LogPoint(DateTimeOffset T, float Value, bool Anomalous);

public record QueueStats(int NormalDepth, int PriorityDepth, long TotalEnqueued, long TotalProcessed, long PriorityProcessed);

public record CommandRequest(string MacAddress, string Action);

public record Suggestion(string Kind, string Title, string Reason, double Confidence, string? Mac, bool? TurnOn);

public record Insight(string Level, string Message, string? Mac);

namespace SmartX.Api.Models;

/// <summary>
/// What a device sends to POST /api/telemetry. Only the MAC and the value,
/// because a real ESP32 would not know about severity or queues.
/// </summary>
public record TelemetryRequest(string MacAddress, float Value);

/// <summary>
/// A telemetry packet after the API has accepted it, waiting in a queue
/// to be processed by TelemetryProcessor.
/// </summary>
public class TelemetryMessage
{
    public string MacAddress { get; set; } = string.Empty;
    public float Value { get; set; }
    public Severity Severity { get; set; }

    // Lower number = more urgent. Only used by the priority queue.
    public int Priority { get; set; }

    public DateTimeOffset ReceivedAt { get; set; } = DateTimeOffset.UtcNow;
}

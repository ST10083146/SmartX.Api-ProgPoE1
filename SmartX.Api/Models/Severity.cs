namespace SmartX.Api.Models;

/// <summary>How serious a single reading is. Normal readings go in the standard queue,
/// Critical readings go in the priority queue.</summary>
public enum Severity
{
    Normal,
    Warning,
    Critical
}

/// <summary>Overall state of a device on the dashboard.</summary>
public enum DeviceStatus
{
    Online,
    Alert,
    Disconnected
}

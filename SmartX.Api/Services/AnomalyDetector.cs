using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Simple threshold-based anomaly check per sensor category. Flags feed the
/// dashboard's live engagement strip (spike badges) rather than a static
/// progress bar.
/// </summary>
public sealed class AnomalyDetector
{
    public bool IsAnomalous(SensorCategory category, float value) => category switch
    {
        SensorCategory.Environmental => value is < 5f or > 90f,      // soil moisture % out of realistic band
        SensorCategory.PowerConsumption => value is < 0f or > 3000f, // watts outside expected draw
        SensorCategory.Actuator => false,                            // booleans surfaced separately, never "anomalous" per se
        _ => false
    };
}

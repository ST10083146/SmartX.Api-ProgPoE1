using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Simple threshold check per sensor category.
/// Part 1 only had "anomalous or not". For Part 2 I added three levels
/// (Normal / Warning / Critical) so critical readings can use the priority queue.
/// </summary>
public class AnomalyDetector
{
    public Severity Classify(SensorCategory category, float value)
    {
        if (category == SensorCategory.Environmental)
        {
            // soil moisture in %
            if (value < 20f || value > 95f) return Severity.Critical;
            if (value < 30f || value > 85f) return Severity.Warning;
        }
        else if (category == SensorCategory.PowerConsumption)
        {
            // power draw in watts
            if (value < 0f || value > 3000f) return Severity.Critical;
            if (value > 2200f) return Severity.Warning;
        }

        // Actuators report 0 or 1, never an anomaly.
        return Severity.Normal;
    }

    /// <summary>
    /// The limits where a reading turns into a Warning. The trend predictor uses these to work out
    /// whether a value is heading towards trouble. Returns null for categories with no band (actuators).
    /// </summary>
    public (float low, float high)? GetWarningBand(SensorCategory category)
    {
        if (category == SensorCategory.Environmental) return (30f, 85f);
        if (category == SensorCategory.PowerConsumption) return (0f, 2200f);
        return null;
    }

    // Kept from Part 1 so the older code still works.
    public bool IsAnomalous(SensorCategory category, float value)
    {
        return Classify(category, value) != Severity.Normal;
    }

    /// <summary>
    /// How far outside the "safe" band a critical reading is. A bigger number means
    /// a worse problem, which is used to order readings inside the priority queue.
    /// </summary>
    public float Deviation(SensorCategory category, float value)
    {
        if (category == SensorCategory.Environmental)
        {
            if (value < 20f) return 20f - value;
            if (value > 95f) return value - 95f;
        }
        else if (category == SensorCategory.PowerConsumption)
        {
            if (value > 3000f) return (value - 3000f) / 10f; // scaled down so it is comparable with %
            if (value < 0f) return -value;
        }
        return 0f;
    }
}

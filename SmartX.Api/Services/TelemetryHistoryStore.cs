using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Buffers raw telemetry as a jagged array (each row is one ingestion
/// batch, rows can be different lengths since not every poll returns the
/// same number of readings) and then transfers that batch into an
/// optimised List&lt;TelemetryPacket&lt;float&gt;&gt; per sensor for fast,
/// resizable historical queries. This is the concrete "jagged array ->
/// List&lt;T&gt;" pipeline required by the brief.
/// </summary>
public sealed class TelemetryHistoryStore
{
    private readonly ConcurrentDictionary<Guid, List<TelemetryPacket<float>>> _history = new();

    /// <summary>
    /// Accepts a jagged batch of raw readings (rawBatch[row] = one burst of
    /// values collected in a single poll cycle) and flushes every value
    /// into the sensor's optimised List&lt;T&gt; history.
    /// </summary>
    public void IngestBatch(Guid sensorId, float[][] rawBatch, string unit, AnomalyDetector detector, SensorCategory category)
    {
        var list = _history.GetOrAdd(sensorId, _ => new List<TelemetryPacket<float>>());

        // Walk the jagged array row by row, column by column — rows can be
        // uneven lengths, which a rectangular float[,] could not represent.
        for (var row = 0; row < rawBatch.Length; row++)
        {
            var batchRow = rawBatch[row];
            for (var col = 0; col < batchRow.Length; col++)
            {
                var packet = new TelemetryPacket<float>(sensorId, batchRow[col], unit)
                {
                    IsAnomalous = detector.IsAnomalous(category, batchRow[col])
                };
                list.Add(packet);
            }
        }

        // Cap history so a long-running demo doesn't grow unbounded.
        const int maxRetained = 500;
        if (list.Count > maxRetained)
        {
            list.RemoveRange(0, list.Count - maxRetained);
        }
    }

    public IReadOnlyList<TelemetryPacket<float>> GetHistory(Guid sensorId) =>
        _history.TryGetValue(sensorId, out var list) ? list : Array.Empty<TelemetryPacket<float>>();
}

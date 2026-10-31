using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Historical readings for every device, kept in time order.
/// Each device gets its own SortedDictionary where the key is the timestamp (in ticks),
/// so the frontend can ask for the latest N readings already sorted and draw a timeline graph
/// without sorting anything itself.
///
/// <para>References:</para>
///   Microsoft (n.d.f) SortedDictionary&lt;TKey,TValue&gt; Class.
///     Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.sorteddictionary-2 (Accessed: 10 October 2026).
///   GeeksforGeeks (n.d.) SortedDictionary Class in C#.
///     Available at: https://www.geeksforgeeks.org/sorteddictionary-class-in-c-sharp (Accessed: 10 October 2026).
/// </summary>
public class TelemetryLog
{
    private const int MaxReadingsPerDevice = 300;

    // MAC address -> (timestamp ticks -> reading)
    private readonly Dictionary<string, SortedDictionary<long, TelemetryPacket<float>>> _logs =
        new Dictionary<string, SortedDictionary<long, TelemetryPacket<float>>>();

    // SortedDictionary is not thread-safe either (GeeksforGeeks notes this).
    private readonly object _lock = new object();

    public void Add(string mac, TelemetryPacket<float> packet)
    {
        lock (_lock)
        {
            if (!_logs.TryGetValue(mac, out var sorted))
            {
                sorted = new SortedDictionary<long, TelemetryPacket<float>>();
                _logs[mac] = sorted;
            }

            // Keys must be unique. If two readings land on the exact same tick,
            // nudge the key by one tick so the second one does not crash Add().
            long key = packet.Timestamp.UtcTicks;
            while (sorted.ContainsKey(key))
            {
                key++;
            }
            sorted.Add(key, packet);

            // Keep memory under control: drop the oldest reading (the first key).
            if (sorted.Count > MaxReadingsPerDevice)
            {
                long oldest = sorted.Keys.First();
                sorted.Remove(oldest);
            }
        }
    }

    /// <summary>The newest N readings for one device, oldest first.</summary>
    public List<TelemetryPacket<float>> GetLatest(string mac, int take)
    {
        lock (_lock)
        {
            if (!_logs.TryGetValue(mac, out var sorted))
            {
                return new List<TelemetryPacket<float>>();
            }
            return sorted.Values.TakeLast(take).ToList();
        }
    }
}

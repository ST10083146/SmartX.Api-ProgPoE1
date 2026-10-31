using System.Diagnostics;
using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// The long-term history of every sensor. Each sensor gets its OWN AvlTree keyed by timestamp
/// (in ticks). This is what makes "all moisture readings between 14:00 and 16:00 for Node A"
/// fast: the tree jumps straight to the 14:00 area instead of reading every reading ever stored.
///
/// (Part 2's TelemetryLog is a SortedDictionary kept for the live timeline graph. This archive is
/// the Part 3 historical store.)
/// </summary>
public class TelemetryArchive
{
    private readonly Dictionary<string, AvlTree<long, TelemetryPacket<float>>> _trees =
        new Dictionary<string, AvlTree<long, TelemetryPacket<float>>>();

    // The tree is not thread-safe, and the processor writes while the API reads.
    private readonly object _lock = new object();

    public void Add(string mac, TelemetryPacket<float> packet)
    {
        lock (_lock)
        {
            if (!_trees.TryGetValue(mac, out var tree))
            {
                tree = new AvlTree<long, TelemetryPacket<float>>();
                _trees[mac] = tree;
            }

            // keys must be unique, so nudge by one tick if two readings share a timestamp
            var key = packet.Timestamp.UtcTicks;
            while (tree.TryGet(key, out _))
            {
                key++;
            }
            tree.Insert(key, packet);
        }
    }

    /// <summary>Range query: every reading between from and to (inclusive), oldest first.</summary>
    public (List<TelemetryPacket<float>> Readings, int NodesVisited, int TreeSize, int TreeHeight, double Microseconds) Query(
        string mac, DateTimeOffset from, DateTimeOffset to)
    {
        lock (_lock)
        {
            if (!_trees.TryGetValue(mac, out var tree))
            {
                return (new List<TelemetryPacket<float>>(), 0, 0, 0, 0);
            }

            var watch = Stopwatch.StartNew();
            var readings = tree.RangeQuery(from.UtcTicks, to.UtcTicks, out var visited);
            watch.Stop();

            var microseconds = watch.ElapsedTicks * 1_000_000.0 / Stopwatch.Frequency;
            return (readings, visited, tree.Count, tree.Height, microseconds);
        }
    }

    public int Count(string mac)
    {
        lock (_lock)
        {
            return _trees.TryGetValue(mac, out var tree) ? tree.Count : 0;
        }
    }
}

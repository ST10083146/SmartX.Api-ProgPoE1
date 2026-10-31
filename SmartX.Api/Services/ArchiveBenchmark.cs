using System.Diagnostics;

namespace SmartX.Api.Services;

/// <summary>
/// Proves the AVL tree is worth it. It builds a big fake telemetry history (inserted in random order),
/// then answers the same "give me this time window" question twice:
///   1. with the AVL tree range query, and
///   2. by looping through a plain list and checking every reading.
/// It returns how long each took, so the speed difference is visible on the dashboard.
/// </summary>
public static class ArchiveBenchmark
{
    public static Models.BenchmarkResult Run(int readings)
    {
        readings = Math.Clamp(readings, 1_000, 1_000_000);

        // one reading per second, then shuffled so the tree has to balance itself
        var timestamps = new long[readings];
        for (var i = 0; i < readings; i++)
        {
            timestamps[i] = i * TimeSpan.TicksPerSecond;
        }

        var random = new Random(12345);
        for (var i = timestamps.Length - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (timestamps[i], timestamps[j]) = (timestamps[j], timestamps[i]);
        }

        var tree = new AvlTree<long, float>();
        var list = new List<(long Time, float Value)>(readings);
        foreach (var time in timestamps)
        {
            var value = (float)random.NextDouble() * 100f;
            tree.Insert(time, value);
            list.Add((time, value));
        }

        // ask for a 2-hour window in the middle of the history
        var low = (readings / 2) * TimeSpan.TicksPerSecond;
        var high = low + 2 * TimeSpan.TicksPerHour;

        const int repeats = 20;

        var visited = 0;
        var found = 0;
        var avlWatch = Stopwatch.StartNew();
        for (var i = 0; i < repeats; i++)
        {
            found = tree.RangeQuery(low, high, out visited).Count;
        }
        avlWatch.Stop();

        var linearFound = 0;
        var linearWatch = Stopwatch.StartNew();
        for (var i = 0; i < repeats; i++)
        {
            linearFound = 0;
            foreach (var item in list)
            {
                if (item.Time >= low && item.Time <= high) linearFound++;
            }
        }
        linearWatch.Stop();

        var avlMicros = avlWatch.ElapsedTicks * 1_000_000.0 / Stopwatch.Frequency / repeats;
        var linearMicros = linearWatch.ElapsedTicks * 1_000_000.0 / Stopwatch.Frequency / repeats;

        return new Models.BenchmarkResult(
            readings,
            found,
            tree.Height,
            tree.IsValid() && found == linearFound,
            visited,
            Math.Round(avlMicros, 1),
            Math.Round(linearMicros, 1),
            avlMicros > 0 ? Math.Round(linearMicros / avlMicros, 1) : 0);
    }
}

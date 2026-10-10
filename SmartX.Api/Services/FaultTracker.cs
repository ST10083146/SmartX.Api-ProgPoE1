using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Remembers which devices currently have an error, using a HashSet.
/// A HashSet only stores unique items, so adding the same fault twice does nothing.
/// Add() returns false the second time, and that is how I stop the system raising
/// duplicate disconnect alerts for the same sensor.
///
/// <para>References:</para>
///   Microsoft (n.d.g) HashSet&lt;T&gt; Class.
///     Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1 (Accessed: 10 October 2026).
/// </summary>
public class FaultTracker
{
    // Entries look like "AA:BB:CC:00:11:22|Disconnected" or "AA:BB:CC:00:11:22|OutOfRange"
    private readonly HashSet<string> _activeFaults = new HashSet<string>();
    private readonly object _lock = new object();

    /// <summary>Returns true only if this fault was NOT already active (so a new alert is needed).</summary>
    public bool Raise(string mac, string faultType)
    {
        lock (_lock)
        {
            return _activeFaults.Add(mac + "|" + faultType);
        }
    }

    /// <summary>Returns true if the fault was active and has now been cleared.</summary>
    public bool Clear(string mac, string faultType)
    {
        lock (_lock)
        {
            return _activeFaults.Remove(mac + "|" + faultType);
        }
    }

    public List<(string Mac, string Type)> GetAll()
    {
        lock (_lock)
        {
            var result = new List<(string Mac, string Type)>();
            foreach (var entry in _activeFaults)
            {
                var parts = entry.Split('|');
                result.Add((parts[0], parts[1]));
            }
            return result;
        }
    }

    public bool HasAnyFault(string mac)
    {
        lock (_lock)
        {
            return _activeFaults.Any(f => f.StartsWith(mac + "|"));
        }
    }
}

namespace SmartX.Api.Services;

/// <summary>
/// Lets the dashboard tell the device simulator to "misbehave" so I can demo the alerts:
/// send a bad reading (spike), or go silent (disconnect).
/// The simulator still sends everything through the normal HTTP API afterwards.
/// </summary>
public class SimulatorControls
{
    private readonly HashSet<string> _silenced = new HashSet<string>();
    // MAC -> how many more reports will be bad (a real fault lasts a while, not one reading)
    private readonly Dictionary<string, int> _spikeTicksLeft = new Dictionary<string, int>();
    // Devices that are slowly getting worse (a creeping fault) instead of failing all at once.
    private readonly HashSet<string> _drifting = new HashSet<string>();
    private readonly object _lock = new object();

    public void Silence(string mac)
    {
        lock (_lock) { _silenced.Add(mac); }
    }

    public void Resume(string mac)
    {
        lock (_lock) { _silenced.Remove(mac); }
    }

    public bool IsSilenced(string mac)
    {
        lock (_lock) { return _silenced.Contains(mac); }
    }

    public void RequestSpike(string mac)
    {
        lock (_lock) { _spikeTicksLeft[mac] = 8; } // 8 reports is about 16 seconds
    }

    /// <summary>Returns true while this device should still be sending bad readings.</summary>
    public bool TakeSpike(string mac)
    {
        lock (_lock)
        {
            if (_spikeTicksLeft.TryGetValue(mac, out var left) && left > 0)
            {
                _spikeTicksLeft[mac] = left - 1;
                return true;
            }
            return false;
        }
    }

    public void CancelSpike(string mac)
    {
        lock (_lock) { _spikeTicksLeft.Remove(mac); }
    }

    public void StartDrift(string mac)
    {
        lock (_lock) { _drifting.Add(mac); }
    }

    public void StopDrift(string mac)
    {
        lock (_lock) { _drifting.Remove(mac); }
    }

    public bool IsDrifting(string mac)
    {
        lock (_lock) { return _drifting.Contains(mac); }
    }
}

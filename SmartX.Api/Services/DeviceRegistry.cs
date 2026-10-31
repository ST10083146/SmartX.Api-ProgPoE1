using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Keeps every registered device in a Dictionary where the MAC address is the key.
/// That means when a telemetry packet arrives I can find its device straight away
/// (O(1) lookup) instead of looping through a list of all devices.
///
/// <para>References:</para>
///   Microsoft (n.d.e) Dictionary&lt;TKey,TValue&gt; Class.
///     Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2 (Accessed: 10 October 2026).
/// </summary>
public class DeviceRegistry
{
    private readonly Dictionary<string, DeviceState> _devices = new Dictionary<string, DeviceState>();

    // Dictionary is not thread-safe, and the API endpoints, the processor and the
    // simulator all use it at the same time, so every method locks.
    private readonly object _lock = new object();

    public static string NormaliseMac(string mac)
    {
        return (mac ?? string.Empty).Trim().ToUpperInvariant();
    }

    public bool Contains(string mac)
    {
        lock (_lock)
        {
            return _devices.ContainsKey(NormaliseMac(mac));
        }
    }

    public DeviceInfo Add(Sensor sensor)
    {
        var state = new DeviceState
        {
            Sensor = sensor,
            Unit = UnitFor(sensor.Category),
            LastContact = DateTimeOffset.UtcNow
        };

        lock (_lock)
        {
            _devices[NormaliseMac(sensor.MacAddress)] = state;
            return ToInfo(state);
        }
    }

    public bool TryGet(string mac, out DeviceInfo? info)
    {
        lock (_lock)
        {
            if (_devices.TryGetValue(NormaliseMac(mac), out var state))
            {
                info = ToInfo(state);
                return true;
            }
        }
        info = null;
        return false;
    }

    public List<DeviceInfo> GetAll()
    {
        lock (_lock)
        {
            return _devices.Values.Select(ToInfo).ToList();
        }
    }

    /// <summary>
    /// Filters devices by free-text search, category and status.
    /// </summary>
    public List<DeviceInfo> Search(string? q, string? category, string? state)
    {
        var results = GetAll();

        if (!string.IsNullOrWhiteSpace(category) &&
            Enum.TryParse<SensorCategory>(category, true, out var parsedCategory))
        {
            results = results.Where(d => d.Category == parsedCategory.ToString()).ToList();
        }

        if (!string.IsNullOrWhiteSpace(state))
        {
            if (state.Equals("alert", StringComparison.OrdinalIgnoreCase))
                results = results.Where(d => d.Status == "Alert").ToList();
            else if (state.Equals("disconnected", StringComparison.OrdinalIgnoreCase))
                results = results.Where(d => d.Status == "Disconnected").ToList();
            else if (state.Equals("online", StringComparison.OrdinalIgnoreCase))
                results = results.Where(d => d.Status == "Online").ToList();
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            results = results.Where(d =>
                d.NodeId.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                d.Mac.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                d.Zone.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                d.Room.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                d.Category.Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        return results.OrderBy(d => d.NodeId).ToList();
    }

    /// <summary>
    /// Called by the processor for every reading it takes off a queue.
    /// Returns the new device info and whether the device was disconnected before this reading.
    /// </summary>
    public (DeviceInfo? info, bool wasDisconnected) ApplyReading(string mac, float value, Severity severity, DateTimeOffset when)
    {
        lock (_lock)
        {
            if (!_devices.TryGetValue(NormaliseMac(mac), out var state))
            {
                return (null, false);
            }

            var wasDisconnected = state.Status == DeviceStatus.Disconnected;

            state.LastValue = value;
            state.LastSeverity = severity;
            state.LastContact = when;
            state.Status = severity == Severity.Normal ? DeviceStatus.Online : DeviceStatus.Alert;

            return (ToInfo(state), wasDisconnected);
        }
    }

    /// <summary>
    /// Finds devices that have not sent anything for a while and marks them Disconnected.
    /// Returns only the ones that were newly marked.
    /// </summary>
    public List<DeviceInfo> MarkSilentDevices(TimeSpan timeout)
    {
        var newlyDisconnected = new List<DeviceInfo>();
        var now = DateTimeOffset.UtcNow;

        lock (_lock)
        {
            foreach (var state in _devices.Values)
            {
                if (state.Status != DeviceStatus.Disconnected && now - state.LastContact > timeout)
                {
                    state.Status = DeviceStatus.Disconnected;
                    newlyDisconnected.Add(ToInfo(state));
                }
            }
        }

        return newlyDisconnected;
    }

    // ---- actuator commands -------------------------------------------------

    public bool TryGetCommandedState(string mac, out bool on)
    {
        lock (_lock)
        {
            if (_devices.TryGetValue(NormaliseMac(mac), out var state))
            {
                on = state.CommandedOn;
                return true;
            }
        }
        on = false;
        return false;
    }

    public DeviceInfo? SetCommandedState(string mac, bool on)
    {
        lock (_lock)
        {
            if (!_devices.TryGetValue(NormaliseMac(mac), out var state))
            {
                return null;
            }
            state.CommandedOn = on;
            return ToInfo(state);
        }
    }

    /// <summary>MACs of all actuators that are currently commanded ON.</summary>
    public List<string> GetActuatorsThatAreOn()
    {
        lock (_lock)
        {
            return _devices
                .Where(pair => pair.Value.Sensor.Category == SensorCategory.Actuator && pair.Value.CommandedOn)
                .Select(pair => pair.Key)
                .ToList();
        }
    }

    /// <summary>Latest wattage of every power meter that has reported at least once.</summary>
    public List<PowerReading> GetLatestPowerReadings()
    {
        lock (_lock)
        {
            return _devices.Values
                .Where(d => d.Sensor.Category == SensorCategory.PowerConsumption && d.LastValue.HasValue)
                .Select(d => new PowerReading(d.LastValue!.Value, d.Sensor.NodeId))
                .ToList();
        }
    }

    // ---- helpers -----------------------------------------------------------

    public static string UnitFor(SensorCategory category)
    {
        if (category == SensorCategory.Environmental) return "%";
        if (category == SensorCategory.PowerConsumption) return "W";
        return string.Empty;
    }

    private static DeviceInfo ToInfo(DeviceState state)
    {
        return new DeviceInfo(
            NormaliseMac(state.Sensor.MacAddress),
            state.Sensor.NodeId,
            state.Sensor.Zone,
            state.Sensor.Room,
            state.Sensor.Category.ToString(),
            state.Status.ToString(),
            state.LastSeverity.ToString(),
            state.LastValue,
            state.Unit,
            state.LastContact,
            state.CommandedOn,
            state.Sensor.Id);
    }
}

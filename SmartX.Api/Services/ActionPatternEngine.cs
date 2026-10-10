using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// The "Predictive Action and Recommendation Engine".
///
/// How it works (kept simple on purpose):
///  1. Every time an alert is raised on a device I note it (RecordAlert).
///  2. When the operator then searches for something or sends a command within a few
///     minutes, I count that action as something that "follows" that alert (RecordUserAction).
///  3. Later, when the SAME device raises an alert again, I look up what the operator
///     usually did last time. If they did the same thing at least twice, I suggest it
///     on the dashboard before they have to search for it.
///
/// Data structures: a Dictionary of Dictionaries counts the patterns, and a HashSet makes sure
/// the same action is only counted once per alert.
///
/// This is a frequency-count (association) approach. It is the simple version of the idea that
/// "people who did B after A are likely to do it again", which is how many recommendation
/// systems start out.
///
/// <para>Attribution: the pattern-counting and trend ideas are my own simple design built from the standard
/// collections below (no tutorial code was copied). The slope uses the standard least-squares formula.</para>
/// <para>References:</para>
///   Microsoft (n.d.e) Dictionary&lt;TKey,TValue&gt; Class.
///     Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.dictionary-2 (Accessed: 10 October 2026).
///   Microsoft (n.d.g) HashSet&lt;T&gt; Class.
///     Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.hashset-1 (Accessed: 10 October 2026).
/// </summary>
public class ActionPatternEngine
{
    private const int MinimumRepeats = 2;
    private static readonly TimeSpan FollowWindow = TimeSpan.FromMinutes(3);

    private readonly DeviceRegistry _devices;
    private readonly FaultTracker _faults;
    private readonly TelemetryLog _log;
    private readonly AnomalyDetector _detector;
    private readonly object _lock = new object();

    // Early-warning settings: look at the last 6 readings and warn if the value would reach its
    // warning limit within the next 10 readings.
    private const int TrendWindow = 6;
    private const float LookAheadReadings = 10f;

    // "alert|MAC" -> number of times that alert has happened
    private readonly Dictionary<string, int> _alertCounts = new Dictionary<string, int>();

    // "alert|MAC" -> ( "cmd|MAC|on" or "search|term" -> number of times the operator did it afterwards )
    private readonly Dictionary<string, Dictionary<string, int>> _followers =
        new Dictionary<string, Dictionary<string, int>>();

    private string? _lastAlertKey;
    private DateTimeOffset _lastAlertTime;
    private readonly HashSet<string> _actionsSinceLastAlert = new HashSet<string>();

    public ActionPatternEngine(DeviceRegistry devices, FaultTracker faults, TelemetryLog log, AnomalyDetector detector)
    {
        _devices = devices;
        _faults = faults;
        _log = log;
        _detector = detector;
    }

    public void RecordAlert(string mac)
    {
        lock (_lock)
        {
            var key = "alert|" + mac;
            _alertCounts.TryGetValue(key, out var count);
            _alertCounts[key] = count + 1;

            _lastAlertKey = key;
            _lastAlertTime = DateTimeOffset.UtcNow;
            _actionsSinceLastAlert.Clear();
        }
    }

    public void RecordUserAction(string actionKey)
    {
        lock (_lock)
        {
            if (_lastAlertKey == null || DateTimeOffset.UtcNow - _lastAlertTime > FollowWindow)
            {
                return; // nothing recent for this action to be "following"
            }

            // Only count each kind of action once per alert, otherwise clicking a button
            // five times would look like five separate patterns.
            if (!_actionsSinceLastAlert.Add(actionKey))
            {
                return;
            }

            if (!_followers.TryGetValue(_lastAlertKey, out var counts))
            {
                counts = new Dictionary<string, int>();
                _followers[_lastAlertKey] = counts;
            }

            counts.TryGetValue(actionKey, out var current);
            counts[actionKey] = current + 1;
        }
    }

    /// <summary>Suggested actions for the devices that have an active alert right now.</summary>
    public List<Suggestion> GetSuggestions()
    {
        var suggestions = new List<Suggestion>();
        var alertingMacs = _faults.GetAll().Select(f => f.Mac).Distinct().ToList();

        lock (_lock)
        {
            foreach (var mac in alertingMacs)
            {
                var alertKey = "alert|" + mac;
                if (!_followers.TryGetValue(alertKey, out var counts))
                {
                    continue;
                }

                _alertCounts.TryGetValue(alertKey, out var occurrences);

                // The alert that is active right now hasn't been "reacted to" yet, so
                // only count the earlier ones when working out how often the operator reacted.
                occurrences = Math.Max(1, occurrences - 1);

                foreach (var pair in counts)
                {
                    if (pair.Value < MinimumRepeats)
                    {
                        continue;
                    }

                    var confidence = Math.Min(1.0, (double)pair.Value / occurrences);
                    var suggestion = BuildSuggestion(mac, pair.Key, pair.Value, occurrences, confidence);
                    if (suggestion != null)
                    {
                        suggestions.Add(suggestion);
                    }
                }
            }
        }

        return suggestions.OrderByDescending(s => s.Confidence).ToList();
    }

    /// <summary>"Automated Insights": devices that look problematic, even before anyone searches for them.</summary>
    public List<Insight> GetInsights()
    {
        var insights = new List<Insight>();

        foreach (var fault in _faults.GetAll())
        {
            if (!_devices.TryGet(fault.Mac, out var device) || device == null)
            {
                continue;
            }

            if (fault.Type == "Disconnected")
            {
                insights.Add(new Insight("critical", device.NodeId + " has stopped reporting (disconnected).", device.Mac));
            }
            else
            {
                var level = device.Severity == "Critical" ? "critical" : "warning";
                insights.Add(new Insight(level,
                    device.NodeId + " last reported " + device.LastValue?.ToString("F1") + device.Unit + ", which is outside its normal range.",
                    device.Mac));
            }
        }

        // Unstable devices: 3 or more out-of-range readings in their last 10, even if they look fine right now.
        foreach (var device in _devices.GetAll())
        {
            var recent = _log.GetLatest(device.Mac, 10);
            var badReadings = recent.Count(p => p.IsAnomalous);

            if (badReadings >= 3 && !_faults.HasAnyFault(device.Mac))
            {
                insights.Add(new Insight("warning",
                    device.NodeId + " had " + badReadings + " out-of-range readings in its last " + recent.Count + ". It may be unstable.",
                    device.Mac));
            }
        }

        // Early warnings: a device that is still "fine" but is heading for trouble.
        insights.AddRange(GetEarlyWarnings());

        return insights;
    }

    /// <summary>
    /// The proactive part of the engine. It looks at each healthy device's recent history (from the
    /// SortedDictionary log) and fits a straight line through the last few readings. If the line
    /// shows the value moving steadily towards its warning limit, the operator is warned BEFORE the
    /// alert fires, so they can act first.
    /// </summary>
    private List<Insight> GetEarlyWarnings()
    {
        var warnings = new List<Insight>();

        foreach (var device in _devices.GetAll())
        {
            // Devices that already have a fault are covered by the normal alert path.
            if (device.Status == "Disconnected" || _faults.HasAnyFault(device.Mac))
            {
                continue;
            }

            Enum.TryParse<SensorCategory>(device.Category, out var category);
            var band = _detector.GetWarningBand(category);
            if (band == null)
            {
                continue;
            }

            var recent = _log.GetLatest(device.Mac, TrendWindow);
            if (recent.Count < TrendWindow)
            {
                continue;
            }

            var values = recent.Select(p => p.Value).ToList();

            // Only trust a steady trend. Random noise zig-zags up and down, so it fails this check.
            if (!IsSteadyTrend(values))
            {
                continue;
            }

            var slope = CalculateSlope(values);
            var minimumSlope = category == SensorCategory.PowerConsumption ? 40f : 1f;
            if (Math.Abs(slope) < minimumSlope)
            {
                continue;
            }

            var rising = slope > 0;

            // A power meter falling towards 0 W is not a problem, so only watch it going up.
            if (category == SensorCategory.PowerConsumption && !rising)
            {
                continue;
            }

            var limit = rising ? band.Value.high : band.Value.low;
            var latest = values[values.Count - 1];
            var readingsLeft = (limit - latest) / slope;

            if (readingsLeft > 0 && readingsLeft <= LookAheadReadings)
            {
                warnings.Add(new Insight("warning",
                    "Early warning: " + device.NodeId + " is " + (rising ? "rising" : "falling") + " by about " +
                    Math.Abs(slope).ToString("F1") + device.Unit + " per reading and could hit its warning limit in about " +
                    Math.Ceiling(readingsLeft) + " readings.",
                    device.Mac));
            }
        }

        return warnings;
    }

    // True when every reading moves in the same direction as the one before it.
    private static bool IsSteadyTrend(List<float> values)
    {
        var goingUp = true;
        var goingDown = true;

        for (var i = 1; i < values.Count; i++)
        {
            if (values[i] <= values[i - 1]) goingUp = false;
            if (values[i] >= values[i - 1]) goingDown = false;
        }

        return goingUp || goingDown;
    }

    // Slope of the best-fit straight line through the values (standard least-squares formula),
    // measured in "change per reading".
    private static float CalculateSlope(List<float> values)
    {
        var n = values.Count;
        float sumX = 0, sumY = 0, sumXY = 0, sumXX = 0;

        for (var i = 0; i < n; i++)
        {
            sumX += i;
            sumY += values[i];
            sumXY += i * values[i];
            sumXX += i * i;
        }

        var denominator = n * sumXX - sumX * sumX;
        if (denominator == 0) return 0;
        return (n * sumXY - sumX * sumY) / denominator;
    }

    private Suggestion? BuildSuggestion(string alertMac, string actionKey, int timesDone, int occurrences, double confidence)
    {
        _devices.TryGet(alertMac, out var alertDevice);
        var alertName = alertDevice != null ? alertDevice.NodeId : alertMac;
        var reason = "After an alert on " + alertName + ", you did this " + timesDone + " of " + occurrences + " times.";

        var parts = actionKey.Split('|');

        if (parts[0] == "cmd" && parts.Length == 3)
        {
            var turnOn = parts[2] == "on";

            if (parts[1] == "ALL")
            {
                return new Suggestion("command", "Shut down all actuators", reason, confidence, "ALL", false);
            }

            if (_devices.TryGet(parts[1], out var target) && target != null)
            {
                // Don't suggest something that is already done.
                if (target.CommandedOn == turnOn)
                {
                    return null;
                }
                return new Suggestion("command", "Turn " + target.NodeId + (turnOn ? " ON" : " OFF"), reason, confidence, target.Mac, turnOn);
            }
        }
        else if (parts[0] == "search" && parts.Length >= 2)
        {
            return new Suggestion("search", "Check devices matching \"" + parts[1] + "\"", reason, confidence, null, null);
        }

        return null;
    }
}

using System;

namespace SmartX.Api.Models;

/// <summary>
/// Represents a single smart-meter wattage reading.
///
/// Operator overloading requirement: the brief's example is literally
/// "Meter3 = Meter1 + Meter2" for aggregate load, plus delta comparisons
/// between readings. This struct implements +, -, and the comparison
/// operators directly on the domain type so callers write natural
/// arithmetic instead of reaching into a .Watts field everywhere:
///
///   var combined = meter1 + meter2;      // aggregate load
///   var drop     = meterBefore - meterAfter;  // delta since last poll
///   if (meterNow > meterBaseline) { ... }      // spike detection
/// </summary>
public readonly struct PowerReading : IEquatable<PowerReading>, IComparable<PowerReading>
{
    public double Watts { get; }
    public string NodeId { get; }

    public PowerReading(double watts, string nodeId = "aggregate")
    {
        Watts = watts;
        NodeId = nodeId;
    }

    public static PowerReading operator +(PowerReading a, PowerReading b) =>
        new(a.Watts + b.Watts, $"{a.NodeId}+{b.NodeId}");

    public static PowerReading operator -(PowerReading a, PowerReading b) =>
        new(a.Watts - b.Watts, $"{a.NodeId}-{b.NodeId}");

    public static bool operator >(PowerReading a, PowerReading b) => a.Watts > b.Watts;
    public static bool operator <(PowerReading a, PowerReading b) => a.Watts < b.Watts;
    public static bool operator >=(PowerReading a, PowerReading b) => a.Watts >= b.Watts;
    public static bool operator <=(PowerReading a, PowerReading b) => a.Watts <= b.Watts;

    public static bool operator ==(PowerReading a, PowerReading b) => a.Watts.Equals(b.Watts);
    public static bool operator !=(PowerReading a, PowerReading b) => !(a == b);

    public bool Equals(PowerReading other) => Watts.Equals(other.Watts);
    public override bool Equals(object? obj) => obj is PowerReading other && Equals(other);
    public override int GetHashCode() => Watts.GetHashCode();
    public int CompareTo(PowerReading other) => Watts.CompareTo(other.Watts);

    public override string ToString() => $"{Watts:F2}W ({NodeId})";
}

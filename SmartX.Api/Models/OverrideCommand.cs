namespace SmartX.Api.Models;

/// <summary>
/// One manual override sent by the operator, e.g. "turn pump A on".
/// Remembers what every affected device looked like BEFORE the command so
/// the Undo button can put things back.
/// </summary>
public class OverrideCommand
{
    public int Id { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTimeOffset IssuedAt { get; set; } = DateTimeOffset.UtcNow;

    // MAC address -> state before the command (true = on)
    public Dictionary<string, bool> PreviousStates { get; set; } = new Dictionary<string, bool>();
}

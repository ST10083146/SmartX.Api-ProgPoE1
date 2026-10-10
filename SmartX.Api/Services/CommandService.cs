using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// Handles manual override commands (turn a pump or relay on/off).
/// Every command is pushed onto a Stack, so "Undo" simply pops the most recent command
/// and puts the devices back how they were. A stack fits because Undo always reverses the
/// LAST thing that was done (Last-In-First-Out).
///
/// <para>References:</para>
///   Microsoft (n.d.c) Stack&lt;T&gt; Class.
///     Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1 (Accessed: 10 October 2026).
///   Microsoft (n.d.d) Stack&lt;T&gt;.Pop Method.
///     Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.stack-1.pop (Accessed: 10 October 2026).
/// </summary>
public class CommandService
{
    private readonly Stack<OverrideCommand> _history = new Stack<OverrideCommand>();
    private readonly object _lock = new object();
    private readonly DeviceRegistry _devices;
    private int _nextId = 1;

    public CommandService(DeviceRegistry devices)
    {
        _devices = devices;
    }

    /// <summary>Turns ONE actuator on or off.</summary>
    public (bool ok, string message, OverrideCommand? command) Execute(string mac, bool turnOn)
    {
        if (!_devices.TryGet(mac, out var device) || device == null)
        {
            return (false, "Unknown device.", null);
        }

        if (device.Category != SensorCategory.Actuator.ToString())
        {
            return (false, device.NodeId + " is not an actuator, so it cannot take override commands.", null);
        }

        _devices.TryGetCommandedState(mac, out var previous);
        if (previous == turnOn)
        {
            return (false, device.NodeId + " is already " + (turnOn ? "ON" : "OFF") + ".", null);
        }

        var command = new OverrideCommand
        {
            Description = "Turn " + device.NodeId + " " + (turnOn ? "ON" : "OFF")
        };
        command.PreviousStates[device.Mac] = previous;

        _devices.SetCommandedState(mac, turnOn);

        lock (_lock)
        {
            command.Id = _nextId++;
            _history.Push(command);
        }

        return (true, command.Description, command);
    }

    /// <summary>"Shut down all valves": turns off every actuator that is currently ON, as one command.</summary>
    public (bool ok, string message, OverrideCommand? command) ShutDownAll()
    {
        var running = _devices.GetActuatorsThatAreOn();
        if (running.Count == 0)
        {
            return (false, "No actuators are currently ON.", null);
        }

        var command = new OverrideCommand
        {
            Description = "Shut down all actuators (" + running.Count + ")"
        };

        foreach (var mac in running)
        {
            command.PreviousStates[mac] = true;
            _devices.SetCommandedState(mac, false);
        }

        lock (_lock)
        {
            command.Id = _nextId++;
            _history.Push(command);
        }

        return (true, command.Description, command);
    }

    /// <summary>Reverts the most recent command. Returns the devices that changed.</summary>
    public (bool ok, string message, List<DeviceInfo> changed) Undo()
    {
        OverrideCommand? last;

        lock (_lock)
        {
            if (_history.Count == 0)
            {
                return (false, "Nothing to undo.", new List<DeviceInfo>());
            }
            last = _history.Pop();
        }

        var changed = new List<DeviceInfo>();
        foreach (var entry in last.PreviousStates)
        {
            var info = _devices.SetCommandedState(entry.Key, entry.Value);
            if (info != null)
            {
                changed.Add(info);
            }
        }

        return (true, "Undid: " + last.Description, changed);
    }

    /// <summary>Command history, newest first (the order a Stack enumerates in).</summary>
    public List<OverrideCommand> GetHistory()
    {
        lock (_lock)
        {
            return _history.ToList();
        }
    }
}

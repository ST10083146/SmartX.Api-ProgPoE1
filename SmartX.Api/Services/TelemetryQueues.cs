using SmartX.Api.Models;

namespace SmartX.Api.Services;

/// <summary>
/// The two lanes that incoming telemetry waits in before it is processed.
///
///  - Normal lane: a Queue&lt;T&gt;, plain First-In-First-Out.
///  - Priority lane: a PriorityQueue for critical readings (a severe power spike or
///    a critical moisture drop). TelemetryProcessor always empties this lane first,
///    so critical readings skip past everything waiting in the normal lane.
///
/// <para>References:</para>
///   Microsoft (n.d.a) Queue&lt;T&gt; Class.
///     Available at: https://learn.microsoft.com/en-us/dotnet/api/system.collections.generic.queue-1 (Accessed: 10 October 2026).
///   Microsoft (n.d.b) PriorityQueue&lt;TElement,TPriority&gt; Class.
///     Available at: https://learn.microsoft.com/dotnet/api/system.collections.generic.priorityqueue-2 (Accessed: 10 October 2026).
///   Abuhakmeh, K. (n.d.) Working with .NET 6 PriorityQueue.
///     Available at: https://khalidabuhakmeh.com/working-with-dotnet-six-priorityqueue (Accessed: 10 October 2026).
/// </summary>
public class TelemetryQueues
{
    private readonly Queue<TelemetryMessage> _normalLane = new Queue<TelemetryMessage>();

    // In PriorityQueue the element with the SMALLEST priority number comes out first.
    private readonly PriorityQueue<TelemetryMessage, int> _priorityLane = new PriorityQueue<TelemetryMessage, int>();

    // Neither Queue nor PriorityQueue is thread-safe, so all access is inside a lock.
    private readonly object _lock = new object();

    private long _totalEnqueued;
    private long _totalProcessed;
    private long _priorityProcessed;

    /// <summary>Puts a message in the correct lane. Returns "priority" or "normal".</summary>
    public string Enqueue(TelemetryMessage message)
    {
        lock (_lock)
        {
            _totalEnqueued++;

            if (message.Severity == Severity.Critical)
            {
                _priorityLane.Enqueue(message, message.Priority);
                return "priority";
            }

            _normalLane.Enqueue(message);
            return "normal";
        }
    }

    /// <summary>
    /// Takes the next message to process. Priority lane first, then the normal lane.
    /// </summary>
    public bool TryDequeueNext(out TelemetryMessage? message, out bool fromPriorityLane)
    {
        lock (_lock)
        {
            if (_priorityLane.TryDequeue(out var urgent, out _))
            {
                message = urgent;
                fromPriorityLane = true;
                _totalProcessed++;
                _priorityProcessed++;
                return true;
            }

            if (_normalLane.TryDequeue(out var normal))
            {
                message = normal;
                fromPriorityLane = false;
                _totalProcessed++;
                return true;
            }
        }

        message = null;
        fromPriorityLane = false;
        return false;
    }

    public QueueStats GetStats()
    {
        lock (_lock)
        {
            return new QueueStats(_normalLane.Count, _priorityLane.Count, _totalEnqueued, _totalProcessed, _priorityProcessed);
        }
    }
}

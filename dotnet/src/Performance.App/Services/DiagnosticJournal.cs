namespace Performance.App.Services;

public sealed record DiagnosticEntry(DateTimeOffset Date, string Operation, double Milliseconds, string? ErrorType);

public sealed class DiagnosticJournal(TimeProvider clock, int capacity = 200)
{
    private readonly object _gate = new();
    private readonly Queue<DiagnosticEntry> _entries = new();
    public void Record(string operation, TimeSpan elapsed, Exception? exception = null)
    {
        lock (_gate)
        {
            _entries.Enqueue(new(clock.GetUtcNow(), operation, Math.Max(0, elapsed.TotalMilliseconds), exception?.GetType().Name));
            while (_entries.Count > Math.Max(1, capacity)) _entries.Dequeue();
        }
    }
    public IReadOnlyList<DiagnosticEntry> Snapshot()
    {
        lock (_gate) return _entries.ToArray();
    }
    public void Restore(IEnumerable<DiagnosticEntry> entries)
    {
        lock (_gate)
        {
            _entries.Clear();
            foreach (var entry in entries.TakeLast(Math.Max(1, capacity))) _entries.Enqueue(entry);
        }
    }
}

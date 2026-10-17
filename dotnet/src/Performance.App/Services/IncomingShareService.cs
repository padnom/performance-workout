namespace Performance.App.Services;

public sealed record IncomingShare(string? Json, string? Error);

public sealed class IncomingShareService
{
    private readonly object _gate = new();
    private IncomingShare? _pending;
    public event Action? Received;

    public bool HasPending
    {
        get { lock (_gate) return _pending is not null; }
    }

    public void Offer(IncomingShare share)
    {
        lock (_gate) _pending = share;
        Received?.Invoke();
    }

    public IncomingShare? Consume()
    {
        lock (_gate)
        {
            var pending = _pending;
            _pending = null;
            return pending;
        }
    }
}

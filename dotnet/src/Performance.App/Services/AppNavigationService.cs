namespace Performance.App.Services;

public sealed class AppNavigationService
{
    private readonly List<(long Id, int Priority, Func<Task<bool>> Handler)> _handlers = [];
    private readonly object _gate = new();
    private long _nextId;
    private bool _pendingWorkout;
    public event Action? WorkoutRequested;

    public IDisposable RegisterBackHandler(Func<Task<bool>> handler, int priority = 0)
    {
        var id = Interlocked.Increment(ref _nextId);
        lock (_gate) _handlers.Add((id, priority, handler));
        return new Registration(() => { lock (_gate) _handlers.RemoveAll(item => item.Id == id); });
    }

    public async Task<bool> RequestBackAsync()
    {
        (long Id, int Priority, Func<Task<bool>> Handler)[] handlers;
        lock (_gate) handlers = _handlers.ToArray();
        foreach (var item in handlers.OrderByDescending(item => item.Priority).ThenByDescending(item => item.Id))
            if (await item.Handler()) return true;
        return false;
    }

    public void OpenWorkout()
    {
        Action? requested;
        lock (_gate)
        {
            requested = WorkoutRequested;
            _pendingWorkout = requested is null;
        }
        requested?.Invoke();
    }

    public bool ConsumeWorkoutRequest()
    {
        lock (_gate)
        {
            var pending = _pendingWorkout;
            _pendingWorkout = false;
            return pending;
        }
    }

    private sealed class Registration(Action dispose) : IDisposable
    {
        private Action? _dispose = dispose;
        public void Dispose() => Interlocked.Exchange(ref _dispose, null)?.Invoke();
    }
}

using Performance.Domain;
using System.Globalization;

namespace Performance.Application;

public sealed class WorkoutStateService(
    IPerformanceDataRepository repository,
    ITrainingCatalog catalog,
    TimeProvider timeProvider)
{
    private const string WeekProgramId = WorkoutTransforms.WeekProgramId;
    private const string RestNotificationSetting = "training-rest-notifications-enabled";
    private readonly SemaphoreSlim _mutationLock = new(1, 1);
    private readonly object _catalogCacheLock = new();
    private IReadOnlyList<Exercise>? _mergedExerciseSource;
    private IReadOnlyList<Exercise> _mergedExercises = [];
    private IReadOnlyList<Exercise>? _visibleExerciseSource;
    private IReadOnlyList<string>? _hiddenExerciseSource;
    private IReadOnlyList<Exercise> _visibleExercises = [];
    private IReadOnlyList<WorkoutProgram>? _programSource;
    private IReadOnlyList<WorkoutProgram> _mergedPrograms = [];
    private IReadOnlyList<Exercise>? _historyExerciseSource;
    private IReadOnlyList<TrainingSessionLog>? _historySource;
    private WorkoutHistoryIndex? _historyIndex;
    private bool _initialized;

    public PerformanceData Data { get; private set; } = new();
    public event Action<PerformanceData>? DataChanged;
    public bool RestNotificationsEnabled => bool.TryParse(Data.Settings.GetValueOrDefault(RestNotificationSetting), out var enabled) && enabled;
    public IReadOnlyList<Exercise> AllExercises => GetAllExercises(Data.Exercises);
    public IReadOnlyList<Exercise> Exercises => GetVisibleExercises();
    public IReadOnlyList<WorkoutProgram> Programs => GetPrograms();
    public IReadOnlyList<WorkoutSession> WeekSessions => Data.Programs.FirstOrDefault(program => program.Id == WeekProgramId)?.Sessions ?? [];

    public Task<WorkoutResult> AddWeekSessionAsync(string sessionId, CancellationToken cancellationToken = default) =>
        MutateAsync(() => AddWeekSession(sessionId), cancellationToken);

    public Task<WorkoutResult> MoveWeekSessionAsync(int index, int delta, CancellationToken cancellationToken = default) =>
        MutateAsync(() => MoveWeekSession(index, delta), cancellationToken);

    public Task<WorkoutResult> RemoveWeekSessionAsync(int index, CancellationToken cancellationToken = default) =>
        MutateAsync(() => RemoveWeekSession(index), cancellationToken);
    public string CurrentGym => Data.Settings.GetValueOrDefault("training-workout-gym") ?? Data.Settings.GetValueOrDefault("workoutGym") ?? "One Air";

    public Task InitializeAsync(CancellationToken cancellationToken = default) => LoadAsync(false, cancellationToken);
    public Task ReloadAsync(CancellationToken cancellationToken = default) => LoadAsync(true, cancellationToken);

    public string GetExerciseGym(string exerciseId) => ResolveGym(Data.ActiveWorkout, exerciseId);

    private IReadOnlyList<Exercise> GetAllExercises(IReadOnlyList<Exercise> source)
    {
        lock (_catalogCacheLock)
        {
            if (ReferenceEquals(_mergedExerciseSource, source)) return _mergedExercises;
            _mergedExercises = catalog.Exercises.Concat(source).GroupBy(exercise => exercise.Id, StringComparer.Ordinal)
                .Select(group => group.Last()).ToArray();
            _mergedExerciseSource = source;
            return _mergedExercises;
        }
    }

    private IReadOnlyList<Exercise> GetVisibleExercises()
    {
        var data = Data;
        lock (_catalogCacheLock)
        {
            var allExercises = GetAllExercises(data.Exercises);
            if (ReferenceEquals(_visibleExerciseSource, allExercises) && ReferenceEquals(_hiddenExerciseSource, data.HiddenExerciseIds))
                return _visibleExercises;
            _visibleExercises = FilterVisibleExercises(allExercises, data.HiddenExerciseIds);
            _visibleExerciseSource = allExercises;
            _hiddenExerciseSource = data.HiddenExerciseIds;
            return _visibleExercises;
        }
    }

    private static IReadOnlyList<Exercise> FilterVisibleExercises(IReadOnlyList<Exercise> exercises, IReadOnlyList<string> hiddenIds)
    {
        var hidden = hiddenIds.ToHashSet(StringComparer.Ordinal);
        return exercises.Where(exercise => !hidden.Contains(exercise.Id)).ToArray();
    }

    private IReadOnlyList<WorkoutProgram> GetPrograms()
    {
        var source = Data.Programs;
        lock (_catalogCacheLock)
        {
            if (ReferenceEquals(_programSource, source)) return _mergedPrograms;
            _mergedPrograms = catalog.Programs.Concat(source).GroupBy(program => program.Id, StringComparer.Ordinal)
                .Select(group => group.Last()).ToArray();
            _programSource = source;
            return _mergedPrograms;
        }
    }

    public int GetRestSeconds(string exerciseId)
    {
        if (Data.ActiveWorkout?.RestDurations.TryGetValue(exerciseId, out var duration) == true) return duration;
        var plannedRest = Data.ActiveWorkout?.Session?.Exercises.FirstOrDefault(item => item.ExerciseId == exerciseId)?.Rest;
        return RestDuration.Parse(plannedRest ?? AllExercises.FirstOrDefault(exercise => exercise.Id == exerciseId)?.Rest);
    }

    public IReadOnlyList<TrainingSet> GetSets(string exerciseId, string? gym = null)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return [];
        var selectedGym = gym ?? ResolveGym(active, exerciseId);
        if (active.Draft.TryGetValue(DraftKey(exerciseId, selectedGym), out var gymSets)) return gymSets;
        if (selectedGym == ResolveGym(active, exerciseId) && active.Draft.TryGetValue(exerciseId, out var sets)) return sets;
        return Prefill(exerciseId, selectedGym, SetCount(active.Session, exerciseId));
    }

    public Task<WorkoutResult> StartAsync(WorkoutSession session, string gym, CancellationToken cancellationToken = default) =>
        MutateAsync(() => Start(session, gym), cancellationToken);

    public Task<WorkoutResult> UpdateSetAsync(string exerciseId, int index, TrainingSet set, string? gym = null,
        CancellationToken cancellationToken = default) => MutateAsync(() => UpdateSet(exerciseId, index, set, gym), cancellationToken);

    public Task<WorkoutResult> UpdateSetValueAsync(string exerciseId, int index, TrainingSetField field, string value,
        string? gym = null, CancellationToken cancellationToken = default) =>
        MutateAsync(() => UpdateSetValue(exerciseId, index, field, value, gym), cancellationToken);

    public Task<WorkoutResult> SetRestDurationAsync(string exerciseId, int seconds, CancellationToken cancellationToken = default) =>
        MutateAsync(() => SetRestDuration(exerciseId, seconds), cancellationToken);

    public Task<WorkoutResult> StartRestAsync(string exerciseId, CancellationToken cancellationToken = default) =>
        MutateAsync(() => StartRest(exerciseId), cancellationToken);

    public Task<WorkoutResult> ExtendRestAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(ExtendRest, cancellationToken);

    public Task<WorkoutResult> StopRestAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() => ClearRest(false), cancellationToken);

    public Task<WorkoutResult> CompleteRestAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() => ClearRest(true), cancellationToken);

    public Task<WorkoutResult> CompleteRestAsync(DateTimeOffset expectedDeadline, CancellationToken cancellationToken = default) =>
        MutateAsync(() => Data.ActiveWorkout?.RestEnd == expectedDeadline && expectedDeadline <= timeProvider.GetUtcNow()
            ? ClearRest(true) : Change.Failure("StaleRest"), cancellationToken);

    public Task<WorkoutResult> SetRestNotificationsEnabledAsync(bool enabled, CancellationToken cancellationToken = default) =>
        MutateAsync(() => new(Data with { Settings = new Dictionary<string, string>(Data.Settings, StringComparer.Ordinal)
            { [RestNotificationSetting] = enabled.ToString(CultureInfo.InvariantCulture) } }), cancellationToken);

    public Task<WorkoutResult> FinishAsync(CancellationToken cancellationToken = default) => MutateAsync(Finish, cancellationToken);

    public Task<WorkoutResult> AddExerciseAsync(string exerciseId, CancellationToken cancellationToken = default) =>
        MutateAsync(() => AddExercise(exerciseId), cancellationToken);

    public Task<WorkoutResult> ReplaceExerciseAsync(string oldId, string newId, CancellationToken cancellationToken = default) =>
        MutateAsync(() => ReplaceExercise(oldId, newId), cancellationToken);

    public Task<WorkoutResult> RemoveExerciseAsync(string exerciseId, CancellationToken cancellationToken = default) =>
        MutateAsync(() => RemoveExercise(exerciseId), cancellationToken);

    public Task<WorkoutResult> UpdateActiveExerciseAsync(Exercise exercise, CancellationToken cancellationToken = default) =>
        MutateAsync(() => UpdateActiveExercise(exercise), cancellationToken);

    public Task<WorkoutResult> CreateAndAddExerciseAsync(Exercise exercise, CancellationToken cancellationToken = default) =>
        MutateAsync(() => CreateAndAddExercise(exercise), cancellationToken);

    public Task<WorkoutResult> SaveExerciseAsync(Exercise exercise, CancellationToken cancellationToken = default) =>
        MutateAsync(() => SaveExercise(exercise), cancellationToken);

    public Task<WorkoutResult> ReplaceDataAsync(PerformanceData data, CancellationToken cancellationToken = default) =>
        MutateAsync(() => new Change(data), cancellationToken);

    public Task<WorkoutResult> SetGymAsync(string gym, CancellationToken cancellationToken = default) =>
        MutateAsync(() => SetGym(gym), cancellationToken);

    public Task<WorkoutResult> SetExerciseGymAsync(string exerciseId, string gym, CancellationToken cancellationToken = default) =>
        MutateAsync(() => SetExerciseGym(exerciseId, gym), cancellationToken);

    public Task<WorkoutResult> CancelAsync(CancellationToken cancellationToken = default) =>
        MutateAsync(() => Data.ActiveWorkout is null ? Change.Failure("NoActiveWorkout") : new(Data with { ActiveWorkout = null }), cancellationToken);

    private async Task LoadAsync(bool reload, CancellationToken cancellationToken)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try { await LoadCoreAsync(reload, cancellationToken); }
        finally { _mutationLock.Release(); }
    }

    private async Task LoadCoreAsync(bool reload, CancellationToken cancellationToken)
    {
        if (_initialized && !reload) return;
        Data = await repository.LoadAsync(cancellationToken);
        _initialized = true;
        PublishDataChanged();
    }

    private async Task<WorkoutResult> MutateAsync(Func<Change> mutation, CancellationToken cancellationToken)
    {
        await _mutationLock.WaitAsync(cancellationToken);
        try { return await PersistMutationAsync(mutation, cancellationToken); }
        catch (IOException) { return WorkoutResult.Failure("PersistenceFailed"); }
        catch (UnauthorizedAccessException) { return WorkoutResult.Failure("PersistenceFailed"); }
        finally { _mutationLock.Release(); }
    }

    private async Task<WorkoutResult> PersistMutationAsync(Func<Change> mutation, CancellationToken cancellationToken)
    {
        await LoadCoreAsync(false, cancellationToken);
        var change = mutation();
        if (change.Data is null) return WorkoutResult.Failure(change.Error!);
        if (ReferenceEquals(change.Data, Data)) return WorkoutResult.Success();
        await repository.SaveAsync(change.Data, cancellationToken);
        Data = change.Data;
        PublishDataChanged();
        return WorkoutResult.Success();
    }

    private void PublishDataChanged()
    {
        if (DataChanged is not { } observers) return;
        foreach (Action<PerformanceData> observer in observers.GetInvocationList())
        {
            try { observer(Data); }
            catch (Exception exception) { System.Diagnostics.Trace.TraceError("Workout observer failed: {0}", exception.GetType().Name); }
        }
    }

    private Change AddWeekSession(string sessionId)
    {
        var session = Programs.Where(program => program.Id != WeekProgramId).SelectMany(program => program.Sessions)
            .FirstOrDefault(session => session.Id == sessionId);
        if (session is null) return Change.Failure("UnknownSession");
        return UpdateWeek([.. WeekSessions, session]);
    }

    private Change MoveWeekSession(int index, int delta)
    {
        var sessions = WeekSessions.ToArray();
        if (index < 0 || index >= sessions.Length) return Change.Failure("InvalidWeekSessionIndex");
        if (delta is not (-1 or 1)) return Change.Failure("InvalidWeekMove");
        var destination = index + delta;
        if (destination < 0 || destination >= sessions.Length) return Change.Failure("InvalidWeekMove");
        (sessions[index], sessions[destination]) = (sessions[destination], sessions[index]);
        return UpdateWeek(sessions);
    }

    private Change RemoveWeekSession(int index)
    {
        var sessions = WeekSessions;
        if (index < 0 || index >= sessions.Count) return Change.Failure("InvalidWeekSessionIndex");
        return UpdateWeek(sessions.Where((_, position) => position != index).ToArray());
    }

    private Change UpdateWeek(IReadOnlyList<WorkoutSession> sessions)
        => new(WorkoutTransforms.UpdateWeek(Data, sessions));

    private Change Start(WorkoutSession session, string gym)
    {
        if (Data.ActiveWorkout is not null) return Change.Failure("ActiveWorkoutExists");
        if (string.IsNullOrWhiteSpace(gym)) return Change.Failure("InvalidGym");
        if (session.Exercises.Any(item => !AllExercises.Any(exercise => exercise.Id == item.ExerciseId))) return Change.Failure("UnknownExercise");
        if (session.Exercises.Select(item => item.ExerciseId).Distinct(StringComparer.Ordinal).Count() != session.Exercises.Count)
            return Change.Failure("DuplicateExercise");
        var gyms = session.Exercises.ToDictionary(item => item.ExerciseId, item => InitialGym(item.ExerciseId, gym, item.Gym), StringComparer.Ordinal);
        var draft = session.Exercises.ToDictionary(item => DraftKey(item.ExerciseId, gyms[item.ExerciseId]),
            item => Prefill(item.ExerciseId, gyms[item.ExerciseId], SetCount(session, item.ExerciseId)), StringComparer.Ordinal);
        var active = new ActiveWorkout { Session = session, StartedAt = timeProvider.GetUtcNow(), DefaultGym = gym, ExerciseGyms = gyms, Draft = draft };
        return new(Data with { ActiveWorkout = active });
    }

    private Change UpdateSetValue(string exerciseId, int index, TrainingSetField field, string value, string? gym)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        if (!active.Session.Exercises.Any(item => item.ExerciseId == exerciseId)) return Change.Failure("UnknownExercise");
        if (!Enum.IsDefined(field)) return Change.Failure("InvalidSetField");
        if (value is null) return Change.Failure("InvalidSet");
        var selectedGym = gym ?? ResolveGym(active, exerciseId);
        if (string.IsNullOrWhiteSpace(selectedGym)) return Change.Failure("InvalidGym");
        var sets = GetSets(exerciseId, selectedGym);
        if (index < 0 || index >= sets.Count) return Change.Failure("InvalidSetIndex");
        var updated = field switch
        {
            TrainingSetField.Weight => sets[index] with { Weight = value },
            TrainingSetField.Reps => sets[index] with { Reps = value },
            TrainingSetField.Rir => sets[index] with { Rir = value },
            _ => sets[index]
        };
        return UpdateSet(exerciseId, index, updated, selectedGym);
    }

    private Change UpdateSet(string exerciseId, int index, TrainingSet set, string? gym)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        if (!active.Session.Exercises.Any(item => item.ExerciseId == exerciseId)) return Change.Failure("UnknownExercise");
        var selectedGym = gym ?? ResolveGym(active, exerciseId);
        if (string.IsNullOrWhiteSpace(selectedGym)) return Change.Failure("InvalidGym");
        var sets = GetSets(exerciseId, selectedGym).ToArray();
        if (index < 0 || index >= sets.Length) return Change.Failure("InvalidSetIndex");
        if (!IsValidSet(set)) return Change.Failure("InvalidSet");
        var startRest = string.IsNullOrWhiteSpace(sets[index].Reps) && IsCompletedSet(set);
        sets[index] = set;
        var draft = CopyDraft(active);
        draft[DraftKey(exerciseId, selectedGym)] = sets;
        var gyms = new Dictionary<string, string>(active.ExerciseGyms, StringComparer.Ordinal) { [exerciseId] = selectedGym };
        var updated = active with { Draft = draft, ExerciseGyms = gyms };
        if (startRest) updated = WithStartedRest(updated, exerciseId);
        return new(Data with { ActiveWorkout = updated });
    }

    private Change SetRestDuration(string exerciseId, int seconds)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        if (!active.Session.Exercises.Any(item => item.ExerciseId == exerciseId)) return Change.Failure("UnknownExercise");
        if (seconds is < 15 or > 600) return Change.Failure("InvalidRestDuration");
        var durations = new Dictionary<string, int>(active.RestDurations, StringComparer.Ordinal) { [exerciseId] = seconds };
        return new(Data with { ActiveWorkout = active with { RestDurations = durations } });
    }

    private Change StartRest(string exerciseId)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        if (!active.Session.Exercises.Any(item => item.ExerciseId == exerciseId)) return Change.Failure("UnknownExercise");
        return new(Data with { ActiveWorkout = WithStartedRest(active, exerciseId) });
    }

    private ActiveWorkout WithStartedRest(ActiveWorkout active, string exerciseId) => active with
    {
        RestExerciseId = exerciseId,
        RestEnd = timeProvider.GetUtcNow().AddSeconds(GetRestSeconds(exerciseId))
    };

    private Change ExtendRest()
    {
        var active = Data.ActiveWorkout;
        if (active?.RestEnd is not { } deadline || deadline <= timeProvider.GetUtcNow()) return Change.Failure("NoActiveRest");
        if (deadline > DateTimeOffset.MaxValue.AddSeconds(-30)) return Change.Failure("InvalidRestDuration");
        return new(Data with { ActiveWorkout = active with { RestEnd = deadline.AddSeconds(30) } });
    }

    private Change ClearRest(bool onlyExpired)
    {
        var active = Data.ActiveWorkout;
        if (active?.RestEnd is not { } deadline) return new(Data);
        if (onlyExpired && deadline > timeProvider.GetUtcNow()) return new(Data);
        return new(Data with { ActiveWorkout = active with { RestEnd = null, RestExerciseId = null } });
    }

    private Change Finish()
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        var draft = CopyDraft(active);
        if (draft.Values.SelectMany(sets => sets).Any(set => !IsValidSet(set))) return Change.Failure("InvalidSet");
        var performances = active.Session.Exercises.SelectMany(item => BuildPerformances(item.ExerciseId, draft)).ToArray();
        if (performances.Length == 0) return Change.Failure("EmptyWorkout");
        var now = timeProvider.GetUtcNow();
        var elapsed = active.StartedAt == DateTimeOffset.MinValue ? 0 : Math.Max(0, (now - active.StartedAt).TotalMinutes);
        var log = new TrainingSessionLog { Id = Guid.NewGuid().ToString("N"), Date = now, SessionId = active.Session.Id,
            DurationMinutes = (int)Math.Min(int.MaxValue, elapsed), Exercises = performances };
        return new(Data with { History = Data.History.Append(log).ToArray(), ActiveWorkout = null });
    }

    private static IEnumerable<ExercisePerformance> BuildPerformances(string exerciseId,
        IReadOnlyDictionary<string, IReadOnlyList<TrainingSet>> draft)
    {
        var prefix = $"{exerciseId}@@";
        return draft.Where(entry => entry.Key.StartsWith(prefix, StringComparison.Ordinal))
            .Select(entry => new ExercisePerformance
            {
                ExerciseId = exerciseId,
                Gym = entry.Key[prefix.Length..],
                Sets = entry.Value.Where(IsCompletedSet).ToArray()
            })
            .Where(performance => performance.Sets.Count > 0);
    }

    private static bool IsValidSet(TrainingSet set) => IsValidDecimal(set.Weight, decimal.MaxValue)
        && IsValidRepetitions(set.Reps) && IsValidDecimal(set.Rir, 10);

    private static bool IsValidDecimal(string? text, decimal maximum) => string.IsNullOrWhiteSpace(text)
        || decimal.TryParse(text.Trim().Replace(',', '.'), NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign,
            CultureInfo.InvariantCulture, out var value) && value >= 0 && value <= maximum;

    private static bool IsValidRepetitions(string? text) => string.IsNullOrWhiteSpace(text)
        || int.TryParse(text.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value >= 0;

    private static bool IsCompletedSet(TrainingSet set) => IsValidSet(set)
        && int.TryParse(set.Reps?.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var repetitions) && repetitions > 0;

    private Change AddExercise(string exerciseId)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        if (!Exercises.Any(exercise => exercise.Id == exerciseId)) return Change.Failure("UnknownExercise");
        if (active.Session.Exercises.Any(item => item.ExerciseId == exerciseId)) return Change.Failure("DuplicateExercise");
        var exercise = Exercises.First(item => item.Id == exerciseId);
        return new(Data with { ActiveWorkout = AppendExercise(active, exercise) });
    }

    private ActiveWorkout AppendExercise(ActiveWorkout active, Exercise exercise)
    {
        var exerciseId = exercise.Id;
        var session = active.Session! with { Exercises = active.Session!.Exercises.Append(new WorkoutExercise { ExerciseId = exerciseId, Order = active.Session.Exercises.Count }).ToArray() };
        var gym = CompatibleGym(exercise, active.DefaultGym ?? CurrentGym);
        var gyms = new Dictionary<string, string>(active.ExerciseGyms, StringComparer.Ordinal) { [exerciseId] = gym };
        var draft = CopyDraft(active);
        draft[DraftKey(exerciseId, gym)] = Prefill(exerciseId, gym, Math.Clamp(exercise.Sets, 1, 20));
        return active with { Session = session, ExerciseGyms = gyms, Draft = draft };
    }

    private Change RemoveExercise(string exerciseId)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        if (!active.Session.Exercises.Any(item => item.ExerciseId == exerciseId)) return Change.Failure("UnknownExercise");
        return new(Data with { ActiveWorkout = WorkoutTransforms.RemoveExercise(active, exerciseId) });
    }

    private Change ReplaceExercise(string oldId, string newId)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        var position = active.Session.Exercises.ToList().FindIndex(item => item.ExerciseId == oldId);
        var exercise = Exercises.FirstOrDefault(item => item.Id == newId);
        if (position < 0 || exercise is null) return Change.Failure("UnknownExercise");
        if (active.Session.Exercises.Any(item => item.ExerciseId == newId)) return Change.Failure("DuplicateExercise");
        var preferredGym = ResolveGym(active, oldId);
        var cleaned = WorkoutTransforms.RemoveExercise(active, oldId);
        var appended = AppendExercise(cleaned, exercise);
        var gym = CompatibleGym(exercise, preferredGym);
        var gyms = new Dictionary<string, string>(appended.ExerciseGyms, StringComparer.Ordinal) { [newId] = gym };
        var draft = new Dictionary<string, IReadOnlyList<TrainingSet>>(appended.Draft, StringComparer.Ordinal);
        draft.Remove(DraftKey(newId, CompatibleGym(exercise, active.DefaultGym ?? CurrentGym)));
        draft[DraftKey(newId, gym)] = PrefillReplacement(newId, gym, Math.Clamp(exercise.Sets, 1, 20));
        var exercises = active.Session.Exercises.ToArray();
        exercises[position] = new WorkoutExercise { ExerciseId = newId, Order = exercises[position].Order };
        var durations = new Dictionary<string, int>(appended.RestDurations, StringComparer.Ordinal) { [newId] = RestDuration.Parse(exercise.Rest) };
        return new(Data with { ActiveWorkout = appended with
            { Session = WorkoutTransforms.Reorder(active.Session, exercises), ExerciseGyms = gyms, Draft = draft, RestDurations = durations } });
    }

    private Change UpdateActiveExercise(Exercise exercise)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        if (!active.Session.Exercises.Any(item => item.ExerciseId == exercise.Id)) return Change.Failure("UnknownExercise");
        if (!IsValidEditableExercise(exercise)) return Change.Failure("InvalidExercise");
        var saved = exercise with { Name = exercise.Name.Trim(), IsCustom = true };
        var draft = CopyDraft(active);
        foreach (var key in draft.Keys.Where(key => key.StartsWith($"{exercise.Id}@@", StringComparison.Ordinal)).ToArray())
            draft[key] = WorkoutTransforms.Resize(draft[key], saved.Sets);
        var gym = CompatibleGym(saved, ResolveGym(active, exercise.Id));
        var selectedKey = DraftKey(exercise.Id, gym);
        if (!draft.ContainsKey(selectedKey)) draft[selectedKey] = WorkoutTransforms.Resize(GetSets(exercise.Id, gym), saved.Sets);
        var gyms = new Dictionary<string, string>(active.ExerciseGyms, StringComparer.Ordinal) { [exercise.Id] = gym };
        var durations = new Dictionary<string, int>(active.RestDurations, StringComparer.Ordinal) { [exercise.Id] = RestDuration.Parse(saved.Rest) };
        var session = active.Session with { Exercises = active.Session.Exercises.Select(item => item.ExerciseId == saved.Id
            ? item with { Sets = saved.Sets, RepTarget = saved.RepTarget, Rest = saved.Rest } : item).ToArray() };
        return new(Data with { Exercises = SaveDefinition(saved), ActiveWorkout = active with
            { Session = session, Draft = draft, ExerciseGyms = gyms, RestDurations = durations } });
    }

    private Change CreateAndAddExercise(Exercise exercise)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        if (!IsValidEditableExercise(exercise)) return Change.Failure("InvalidExercise");
        var saved = exercise with { Id = string.IsNullOrWhiteSpace(exercise.Id) ? Guid.NewGuid().ToString("N") : exercise.Id, Name = exercise.Name.Trim(), IsCustom = true };
        if (AllExercises.Any(item => item.Id == saved.Id || WorkoutHistoryIndex.NormalizeName(item.Name) == WorkoutHistoryIndex.NormalizeName(saved.Name)))
            return Change.Failure("ExerciseAlreadyExists");
        var appended = AppendExercise(active, saved);
        var durations = new Dictionary<string, int>(appended.RestDurations, StringComparer.Ordinal) { [saved.Id] = RestDuration.Parse(saved.Rest) };
        return new(Data with { Exercises = SaveDefinition(saved), ActiveWorkout = appended with { RestDurations = durations } });
    }

    private IReadOnlyList<Exercise> SaveDefinition(Exercise exercise) =>
        Data.Exercises.Where(item => item.Id != exercise.Id).Append(exercise).ToArray();

    private static bool IsValidEditableExercise(Exercise exercise) =>
        !string.IsNullOrWhiteSpace(exercise.Name) && !exercise.Id.Contains("@@", StringComparison.Ordinal)
        && exercise.Sets is >= 1 and <= 20
        && IsValidEditableRest(exercise.Rest)
        && exercise.Variants.Count > 0 && exercise.Variants.All(variant => !string.IsNullOrWhiteSpace(variant.Gym))
        && (string.IsNullOrWhiteSpace(exercise.Link) || Uri.TryCreate(exercise.Link, UriKind.Absolute, out var link) && link.Scheme is "http" or "https");

    private static bool IsValidEditableRest(string rest)
    {
        var parts = rest.Trim().Split(':');
        return parts.Length == 2 && parts[1].Length == 2
            && int.TryParse(parts[0], NumberStyles.None, CultureInfo.InvariantCulture, out var minutes) && minutes is >= 0 and <= 10
            && int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out var seconds) && seconds is >= 0 and < 60
            && minutes * 60 + seconds is >= 15 and <= 600;
    }

    private static string CompatibleGym(Exercise exercise, string preferredGym)
    {
        var gyms = exercise.Variants.Select(variant => variant.Gym).ToArray();
        if (gyms.Length == 0 || gyms.Contains(preferredGym, StringComparer.Ordinal) || gyms.Contains("Toutes", StringComparer.Ordinal)) return preferredGym;
        return gyms[0];
    }

    private Change SaveExercise(Exercise exercise)
    {
        if (string.IsNullOrWhiteSpace(exercise.Name) || exercise.Sets < 1 || exercise.Sets > 20) return Change.Failure("InvalidExercise");
        var saved = exercise with { Id = string.IsNullOrWhiteSpace(exercise.Id) ? Guid.NewGuid().ToString("N") : exercise.Id, IsCustom = true };
        return new(Data with { Exercises = Data.Exercises.Where(item => item.Id != saved.Id).Append(saved).ToArray() });
    }

    private Change SetGym(string gym)
    {
        if (string.IsNullOrWhiteSpace(gym)) return Change.Failure("InvalidGym");
        var settings = new Dictionary<string, string>(Data.Settings, StringComparer.Ordinal) { ["training-workout-gym"] = gym };
        return new(Data with { Settings = settings, ActiveWorkout = WithDefaultGym(Data.ActiveWorkout, gym) });
    }

    private ActiveWorkout? WithDefaultGym(ActiveWorkout? active, string gym)
    {
        if (active?.Session is null) return active;
        var gyms = active.Session.Exercises.ToDictionary(item => item.ExerciseId,
            item => ResolveGym(active, item.ExerciseId), StringComparer.Ordinal);
        return active with { DefaultGym = gym, ExerciseGyms = gyms, Draft = CopyDraft(active) };
    }

    private Change SetExerciseGym(string exerciseId, string gym)
    {
        var active = Data.ActiveWorkout;
        if (active?.Session is null) return Change.Failure("NoActiveWorkout");
        if (!active.Session.Exercises.Any(item => item.ExerciseId == exerciseId)) return Change.Failure("UnknownExercise");
        if (string.IsNullOrWhiteSpace(gym)) return Change.Failure("InvalidGym");
        var sets = GetSets(exerciseId, gym);
        var gyms = new Dictionary<string, string>(active.ExerciseGyms, StringComparer.Ordinal) { [exerciseId] = gym };
        var draft = CopyDraft(active);
        draft[DraftKey(exerciseId, gym)] = sets;
        return new(Data with { ActiveWorkout = active with { ExerciseGyms = gyms, Draft = draft } });
    }

    private string ResolveGym(ActiveWorkout? active, string exerciseId)
    {
        if (active?.ExerciseGyms.GetValueOrDefault(exerciseId) is { } selectedGym) return selectedGym;
        var plannedGym = active?.Session?.Exercises.FirstOrDefault(item => item.ExerciseId == exerciseId)?.Gym;
        if (plannedGym is { Length: > 0 } and not "Toutes") return plannedGym;
        return active?.DefaultGym ?? CurrentGym;
    }

    private int SetCount(WorkoutSession session, string exerciseId) => Math.Clamp(
        session.Exercises.FirstOrDefault(item => item.ExerciseId == exerciseId)?.Sets ?? AllExercises.FirstOrDefault(item => item.Id == exerciseId)?.Sets ?? 4, 1, 20);

    private string InitialGym(string exerciseId, string preferredGym, string? sessionGym = null)
    {
        var availableGyms = AllExercises.FirstOrDefault(item => item.Id == exerciseId)?.Variants.Select(variant => variant.Gym).ToArray() ?? [];
        if (availableGyms.Contains(preferredGym) || availableGyms.Contains("Toutes")) return preferredGym;
        if (availableGyms.Length > 0) return availableGyms[0];
        return sessionGym is { Length: > 0 } and not "Toutes" ? sessionGym : preferredGym;
    }

    private Dictionary<string, IReadOnlyList<TrainingSet>> CopyDraft(ActiveWorkout active)
    {
        var draft = new Dictionary<string, IReadOnlyList<TrainingSet>>(active.Draft, StringComparer.Ordinal);
        foreach (var exerciseId in active.Session!.Exercises.Select(item => item.ExerciseId))
            NormalizeDraftKey(draft, active, exerciseId);
        return draft;
    }

    private void NormalizeDraftKey(Dictionary<string, IReadOnlyList<TrainingSet>> draft, ActiveWorkout active, string exerciseId)
    {
        if (!draft.Remove(exerciseId, out var sets)) return;
        draft.TryAdd(DraftKey(exerciseId, ResolveGym(active, exerciseId)), sets);
    }

    private IReadOnlyList<TrainingSet> Prefill(string exerciseId, string gym, int count)
    {
        var previous = GetExerciseHistory(exerciseId, gym).FirstOrDefault(entry => entry.Performance.Sets
            .Any(set => !string.IsNullOrWhiteSpace(set.Weight) || !string.IsNullOrWhiteSpace(set.Reps)))?.Performance.Sets ?? [];
        return Enumerable.Range(0, count).Select(index => index < previous.Count ? previous[index] with { Reps = "", Rir = "" } : new TrainingSet()).ToArray();
    }

    private IReadOnlyList<TrainingSet> PrefillReplacement(string exerciseId, string gym, int count)
    {
        var previous = GetPreviousSets(exerciseId, gym);
        return Enumerable.Range(0, count).Select(index => new TrainingSet
        {
            Weight = index < previous.Count && !string.IsNullOrWhiteSpace(previous[index].Weight)
                ? previous[index].Weight : previous.LastOrDefault()?.Weight ?? ""
        }).ToArray();
    }

    public IReadOnlyList<ExerciseHistoryEntry> GetExerciseHistory(string exerciseId, string? gym = null)
    {
        var data = Data;
        lock (_catalogCacheLock)
        {
            return GetHistoryIndex(data).GetHistory(exerciseId, gym);
        }
    }

    private WorkoutHistoryIndex GetHistoryIndex(PerformanceData data)
    {
        if (ReferenceEquals(_historyExerciseSource, data.Exercises) && ReferenceEquals(_historySource, data.History) && _historyIndex is not null)
            return _historyIndex;
        _historyIndex = new WorkoutHistoryIndex(catalog.Exercises.Concat(data.Exercises), data.History);
        _historyExerciseSource = data.Exercises;
        _historySource = data.History;
        return _historyIndex;
    }

    public IReadOnlyList<TrainingSet> GetPreviousSets(string exerciseId, string gym) =>
        GetExerciseHistory(exerciseId, gym).FirstOrDefault()?.Performance.Sets ?? [];

    public decimal? GetExerciseRecord(string exerciseId, string gym)
    {
        var data = Data;
        lock (_catalogCacheLock) { return GetHistoryIndex(data).GetRecord(exerciseId, gym); }
    }

    private static string DraftKey(string exerciseId, string gym) => $"{exerciseId}@@{gym}";
    private sealed record Change(PerformanceData? Data, string? Error = null)
    {
        public static Change Failure(string error) => new(null, error);
    }
}

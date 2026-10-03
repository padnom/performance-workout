using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Performance.Application;
using Performance.App.Pages;
using Performance.App.Services;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.Tests;

public sealed class NotificationSettingsTests
{
    [Fact]
    public void OpeningSettings_DoesNotRequestPermissionsAndShowsSilentOptIn()
    {
        using var setup = new Setup();
        var page = setup.Context.Render<Settings>();
        page.WaitForAssertion(() => page.Find(".notification-settings input").HasAttribute("checked").ShouldBeFalse());
        page.Find(".notification-settings").TextContent.ShouldContain("Aucun son");
        setup.Scheduler.Requests.ShouldBe(0);
        page.Find(".diagnostic-settings").TextContent.ShouldContain("1.1.0 (2)");
    }

    [Fact]
    public void OptingIn_RequestsOnceAndPersistsWhileOptingOutDoesNotRequest()
    {
        using var setup = new Setup();
        var page = setup.Context.Render<Settings>();
        page.WaitForAssertion(() => page.Find(".notification-settings input").ShouldNotBeNull());
        page.Find(".notification-settings input").Change(true);
        page.WaitForAssertion(() => setup.State.RestNotificationsEnabled.ShouldBeTrue());
        setup.Scheduler.Requests.ShouldBe(1);
        page.Find(".notification-settings input").Change(false);
        page.WaitForAssertion(() => setup.State.RestNotificationsEnabled.ShouldBeFalse());
        setup.Scheduler.Requests.ShouldBe(1);
    }

    [Fact]
    public void DeniedPermission_LeavesOptInUncheckedAndShowsStatus()
    {
        using var setup = new Setup();
        setup.Scheduler.Access = new(false, false);
        var page = setup.Context.Render<Settings>();
        page.WaitForAssertion(() => page.Find(".notification-settings input").ShouldNotBeNull());
        var input = page.Find(".notification-settings input");
        // Simulate the browser's checked state changing before the permission result arrives.
        input.SetAttribute("checked", "checked");
        input.Change(true);
        page.WaitForAssertion(() =>
        {
            page.Markup.ShouldContain("Activation impossible");
            page.Find(".notification-settings input").HasAttribute("checked").ShouldBeFalse();
        });
        setup.State.RestNotificationsEnabled.ShouldBeFalse();
    }

    [Fact]
    public void ExportDiagnostics_OnlySharesAfterExplicitClick()
    {
        using var setup = new Setup();
        var page = setup.Context.Render<Settings>();
        page.WaitForAssertion(() => page.Find(".diagnostic-settings button").ShouldNotBeNull());
        setup.Diagnostics.Shares.ShouldBe(0);
        page.Find(".diagnostic-settings button").Click();
        page.WaitForAssertion(() => setup.Diagnostics.Shares.ShouldBe(1));
    }

    private sealed class Setup : IDisposable
    {
        public BunitContext Context { get; } = new();
        public WorkoutStateService State { get; }
        public Scheduler Scheduler { get; } = new();
        public Diagnostics Diagnostics { get; } = new();
        private readonly RestNotificationCoordinator _coordinator;
        public Setup()
        {
            var catalog = new DefaultTrainingCatalog();
            State = new(new Repository(), catalog, TimeProvider.System);
            _coordinator = new(State, Scheduler, new Feedback(), TimeProvider.System, Diagnostics);
            Context.Services.AddSingleton(State);
            Context.Services.AddSingleton(_coordinator);
            Context.Services.AddSingleton<IAppDiagnostics>(Diagnostics);
            Context.Services.AddSingleton<IBackupFiles>(new Files());
            Context.Services.AddSingleton<IReactLocalStorageAdapter>(new ReactLocalStorageMigrationAdapter(catalog));
            Context.Services.AddTransient<ImportPerformanceBackupUseCase>();
            Context.Services.AddTransient<ExportPerformanceBackupUseCase>();
        }
        public void Dispose() { Context.Dispose(); _coordinator.Dispose(); }
    }
    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { _data = data; return ValueTask.CompletedTask; }
    }
    private sealed class Scheduler : IRestNotificationScheduler
    {
        public event Action<DateTimeOffset>? ForegroundRestElapsed { add { } remove { } }
        public bool IsForeground { get; set; }
        public RestNotificationAccess Access { get; set; } = new(true, true);
        public int Requests { get; private set; }
        public Task<RestNotificationAccess> GetAccessAsync() => Task.FromResult(Access);
        public Task<RestNotificationAccess> RequestAccessAsync() { Requests++; return Task.FromResult(Access); }
        public Task ScheduleAsync(RestNotificationRegistration registration) => Task.CompletedTask;
        public Task CancelAsync() => Task.CompletedTask;
        public bool TryClaimCompletion(string token) => true;
    }
    private sealed class Feedback : IWorkoutFeedback { public void NotifyRestCompleted() { } }
    private sealed class Diagnostics : IAppDiagnostics
    {
        public int Shares { get; private set; }
        public string Version => "1.1.0 (2)";
        public void Record(string operation, TimeSpan elapsed, Exception? exception = null) { }
        public Task ShareAsync(CancellationToken cancellationToken = default) { Shares++; return Task.CompletedTask; }
    }
    private sealed class Files : IBackupFiles
    {
        public Task<string?> PickBackupAsync(CancellationToken cancellationToken = default) => Task.FromResult<string?>(null);
        public Task ShareBackupAsync(string json, CancellationToken cancellationToken = default) => Task.CompletedTask;
    }
}

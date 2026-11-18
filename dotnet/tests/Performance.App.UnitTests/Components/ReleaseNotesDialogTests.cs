using Bunit;
using JetBrains.Annotations;
using Microsoft.Extensions.DependencyInjection;
using Performance.App.Components;
using Performance.App.Layout;
using Performance.App.Services;
using Performance.Application;
using Performance.Infrastructure;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Components;

[TestSubject(typeof(ReleaseNotesDialog))]
public sealed class ReleaseNotesDialogTests : IDisposable
{
    private readonly BunitContext _context = new();
    private readonly WorkoutStateService _state;
    private readonly Repository _repository = new();

    public ReleaseNotesDialogTests()
    {
        _state = new(_repository, new DefaultTrainingCatalog(), TimeProvider.System);
        _context.Services.AddSingleton(_state);
        _context.Services.AddSingleton<IApkInstaller>(new Installer("1.5.1"));
    }

    [Fact]
    public async Task AfterUpdate_ShowsNotesOnce_AndRemembersVersion()
    {
        _repository.Data = new PerformanceData { Settings = new Dictionary<string, string> { [ReleaseNotes.SeenSetting] = "1.5.0" } };
        await _state.InitializeAsync();

        var layout = _context.Render<MainLayout>();

        layout.Find("#release-notes-title").TextContent.ShouldBe("Nouveautés de la version 1.5.1");
        layout.FindAll(".release-notes li").Count.ShouldBe(ReleaseNotes.For("1.5.1")!.Items.Count);
        layout.Find(".close-release-notes").Click();
        layout.WaitForAssertion(() => layout.FindAll(".release-notes").ShouldBeEmpty());
        _repository.Data.Settings[ReleaseNotes.SeenSetting].ShouldBe("1.5.1");

        var reopened = _context.Render<MainLayout>();
        reopened.FindAll(".release-notes").ShouldBeEmpty();
    }

    [Fact]
    public async Task FirstVersionWithNotes_ShowsEveryUnseenVersion()
    {
        var layout = _context.Render<MainLayout>();
        layout.FindAll(".release-notes").ShouldBeEmpty();

        await _state.InitializeAsync();

        layout.WaitForAssertion(() => layout.FindAll(".release-notes h3").Select(heading => heading.TextContent).ShouldBe(["Version 1.5.1", "Version 1.5.0"]));
    }

    [Fact]
    public async Task AlreadySeen_ShowsNothing()
    {
        _repository.Data = new PerformanceData { Settings = new Dictionary<string, string> { [ReleaseNotes.SeenSetting] = "1.5.1" } };
        await _state.InitializeAsync();

        _context.Render<MainLayout>().FindAll(".release-notes").ShouldBeEmpty();
    }

    public void Dispose() => _context.Dispose();

    private sealed class Installer(string version) : IApkInstaller
    {
        public string InstalledVersion => version;
        public string DownloadDirectory => "";
        public bool CanInstall => false;
        public Task<ApkInstallResult> InstallAsync(string apkPath) => Task.FromResult(ApkInstallResult.Unavailable);
    }

    private sealed class Repository : IPerformanceDataRepository
    {
        public PerformanceData Data { get; set; } = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(Data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { Data = data; return ValueTask.CompletedTask; }
    }
}

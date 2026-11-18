using JetBrains.Annotations;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(ReleaseNotes))]
public sealed class ReleaseNotesTests
{
    [Fact]
    public void Unseen_NothingRecorded_ShowsVersionsAfterTheLastOneWithoutNotes()
    {
        ReleaseNotes.Unseen(null, "1.5.1").Select(note => note.Version.ToString()).ShouldBe(["1.5.1", "1.5.0"]);
    }

    [Fact]
    public void Unseen_AfterOneUpdate_ShowsOnlyNewerVersions()
    {
        ReleaseNotes.Unseen("1.5.0", "1.5.1").Select(note => note.Version.ToString()).ShouldBe(["1.5.1"]);
    }

    [Theory]
    [InlineData("1.5.1", "1.5.1")]
    [InlineData("1.5.1", "1.5.0")]
    [InlineData(null, "1.4.0")]
    [InlineData(null, "abc")]
    public void Unseen_AlreadySeenOrUnknown_ShowsNothing(string? seen, string installed)
    {
        ReleaseNotes.Unseen(seen, installed).ShouldBeEmpty();
    }

    [Fact]
    public void Entries_AreUniqueNewestFirstAndNotEmpty()
    {
        ReleaseNotes.All.Select(note => note.Version).ShouldBeUnique();
        ReleaseNotes.All.Select(note => note.Version).ShouldBe(ReleaseNotes.All.Select(note => note.Version).OrderByDescending(version => version));
        ReleaseNotes.All.ShouldAllBe(note => note.Items.Count > 0 && note.Items.All(item => item.Trim().Length > 0));
    }

    [Fact]
    public void ToMarkdown_ListsItemsUnderVersionHeading()
    {
        var markdown = ReleaseNotes.ToMarkdown(ReleaseNotes.For("android-v1.5.0").ShouldNotBeNull());

        markdown.ShouldStartWith("## Nouveautés de la version 1.5.0\n\n- ");
        markdown.ShouldContain("Refaire cette séance");
    }

    [Fact]
    public async Task MarkReleaseNotesSeen_PersistsInstalledVersion()
    {
        var service = new WorkoutStateService(new Repository(), new EmptyCatalog(), TimeProvider.System);
        await service.InitializeAsync();

        (await service.MarkReleaseNotesSeenAsync("1.5.1")).IsSuccess.ShouldBeTrue();

        service.ReleaseNotesSeenVersion.ShouldBe("1.5.1");
        service.IsInitialized.ShouldBeTrue();
    }

    private sealed class EmptyCatalog : ITrainingCatalog
    {
        public IReadOnlyList<Performance.Domain.Exercise> Exercises { get; } = [];
        public IReadOnlyList<Performance.Domain.WorkoutProgram> Programs { get; } = [];
    }

    private sealed class Repository : IPerformanceDataRepository
    {
        private PerformanceData _data = new();
        public ValueTask<PerformanceData> LoadAsync(CancellationToken cancellationToken = default) => ValueTask.FromResult(_data);
        public ValueTask SaveAsync(PerformanceData data, CancellationToken cancellationToken = default) { _data = data; return ValueTask.CompletedTask; }
    }
}

using System.Text.Json;
using Performance.App.Services;
using Shouldly;
using Xunit;

namespace Performance.App.Tests;

public sealed class AppServicesTests
{
    [Fact]
    public async Task ConcurrentHandlerRegistrationAndBack_UseStableSnapshots()
    {
        var navigation = new AppNavigationService();
        var tasks = Enumerable.Range(0, 100).Select(index => Task.Run(async () =>
        {
            using var registration = navigation.RegisterBackHandler(() => Task.FromResult(false), index);
            (await navigation.RequestBackAsync()).ShouldBeFalse();
        }));
        await Task.WhenAll(tasks);
        (await navigation.RequestBackAsync()).ShouldBeFalse();
    }
    [Fact]
    public async Task BackHandlers_CloseHighestPriorityModalBeforePageAndUnregister()
    {
        var navigation = new AppNavigationService();
        var page = 0;
        using var pageRegistration = navigation.RegisterBackHandler(() => { page++; return Task.FromResult(true); });
        var modal = navigation.RegisterBackHandler(() => Task.FromResult(true), 10);
        (await navigation.RequestBackAsync()).ShouldBeTrue();
        page.ShouldBe(0);
        modal.Dispose();
        (await navigation.RequestBackAsync()).ShouldBeTrue();
        page.ShouldBe(1);
    }
    [Fact]
    public void ColdNotificationTap_IsConsumedOnceAfterNavigationMounts()
    {
        var navigation = new AppNavigationService();
        navigation.OpenWorkout();
        navigation.ConsumeWorkoutRequest().ShouldBeTrue();
        navigation.ConsumeWorkoutRequest().ShouldBeFalse();
    }
    [Fact]
    public void Diagnostics_BoundsHistoryAndExcludesExceptionMessages()
    {
        var journal = new DiagnosticJournal(TimeProvider.System, 2);
        journal.Record("data.load", TimeSpan.Zero);
        journal.Record("data.save", TimeSpan.FromMilliseconds(8), new IOException("private workout values"));
        journal.Record("rest.complete", TimeSpan.FromMilliseconds(1));
        var entries = journal.Snapshot();
        entries.Count.ShouldBe(2);
        entries[0].ErrorType.ShouldBe(nameof(IOException));
        JsonSerializer.Serialize(entries).ShouldNotContain("private workout values");
        entries[0].Milliseconds.ShouldBe(8);
    }
}

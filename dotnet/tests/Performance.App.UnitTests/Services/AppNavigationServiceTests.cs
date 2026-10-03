using JetBrains.Annotations;
using Performance.App.Services;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Services;

[TestSubject(typeof(AppNavigationService))]
public sealed class AppNavigationServiceTests
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
}

using Performance.Application;
using Performance.App.Services;
using Performance.Domain;
using static Performance.App.Resources.Text;

namespace Performance.App.Components;

public sealed class SessionSharer(IServiceProvider services, WorkoutStateService state)
{
    public Task<string> ShareFileAsync(Func<WorkoutSession> build) => ShareAsync(build, async session =>
    {
        var json = Get<ExportWorkoutSessionUseCase>().Execute(session, state.AllExercises);
        await Get<IProgramFiles>().ShareSessionAsync(json, session.Name);
    });

    public Task<string> ShareTextAsync(Func<WorkoutSession> build) => ShareAsync(build, session =>
        Get<IProgramFiles>().ShareTextAsync(SessionText.Format(session, state.AllExercises), session.Name));

    private async Task<string> ShareAsync(Func<WorkoutSession> build, Func<WorkoutSession, Task> share)
    {
        try
        {
            await share(build());
            return T("SessionSharer.Ready");
        }
        catch (FormatException exception)
        {
            return exception.Message;
        }
        catch (Exception exception)
        {
            (services.GetService(typeof(IAppDiagnostics)) as IAppDiagnostics)?.Record("session.share", TimeSpan.Zero, exception);
            return T("SessionSharer.Failed");
        }
    }

    private T Get<T>() where T : class => services.GetService(typeof(T)) as T ?? throw new InvalidOperationException();
}

using Microsoft.Extensions.Logging;

namespace Performance.App.Services;

public sealed class MauiWorkoutFeedback(ILogger<MauiWorkoutFeedback> logger) : IWorkoutFeedback
{
    public void NotifyRestCompleted()
    {
        try
        {
            if (Vibration.Default.IsSupported) Vibration.Default.Vibrate(TimeSpan.FromMilliseconds(200));
        }
        catch (Exception exception)
        {
            logger.LogDebug(exception, "Rest completion vibration is unavailable.");
        }
    }
}

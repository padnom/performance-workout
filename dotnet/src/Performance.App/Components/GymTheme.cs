using Performance.Application;

namespace Performance.App.Components;

/// <summary>Maps the gym the user trains in to the colour theme applied on the app shell.</summary>
public static class GymTheme
{
    public const string Default = "all";

    public static string For(string? gym) => gym switch
    {
        "Basic-Fit" => "basic-fit",
        "One Air" => "one-air",
        _ => Default
    };

    /// <summary>The gym of the session in progress wins over the gym chosen in the settings.</summary>
    public static string CurrentGym(WorkoutStateService state) => state.Data.ActiveWorkout?.DefaultGym ?? state.CurrentGym;
}

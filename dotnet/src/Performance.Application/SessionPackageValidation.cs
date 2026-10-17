namespace Performance.Application;

public static class SessionPackageValidation
{
    public static void Validate(SessionPackage package)
    {
        if (package is null) throw new FormatException("Fichier de séance invalide.");
        Ensure(package.Kind == SessionPackage.DocumentKind && package.SchemaVersion == 1);
        Ensure(ProgramPackageValidation.ValidSession(package.Session) && package.Session.Exercises.Count >= 1);
        Ensure(package.Exercises is { Count: <= 2000 } && package.Exercises.All(ProgramPackageValidation.ValidExercise));
        var defined = package.Exercises.Select(exercise => exercise.Id).ToArray();
        Ensure(defined.Distinct(StringComparer.Ordinal).Count() == defined.Length);
        var referenced = package.Session.Exercises.Concat(package.Session.OptionalExercises)
            .Select(plan => plan.ExerciseId).ToHashSet(StringComparer.Ordinal);
        Ensure(referenced.SetEquals(defined));
    }

    private static void Ensure(bool condition)
    {
        if (!condition) throw new FormatException("Fichier de séance invalide ou version non prise en charge.");
    }
}

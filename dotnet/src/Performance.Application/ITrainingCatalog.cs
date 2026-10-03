using Performance.Domain;

namespace Performance.Application;

public interface ITrainingCatalog
{
    IReadOnlyList<Exercise> Exercises { get; }
    IReadOnlyList<WorkoutProgram> Programs { get; }
}

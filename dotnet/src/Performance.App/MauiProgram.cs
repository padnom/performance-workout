using Microsoft.Extensions.Logging;
using Performance.Application;
using Performance.Infrastructure;
using Performance.App.Services;

namespace Performance.App;

public static class MauiProgram
{
    public static MauiApp CreateMauiApp()
    {
        var builder = MauiApp.CreateBuilder();
        builder
            .UseMauiApp<App>();

        builder.Services.AddMauiBlazorWebView();
        builder.Services.AddSingleton<IJsonDocumentStore>(_ =>
            new JsonFileDocumentStore(Path.Combine(FileSystem.AppDataDirectory, "performance-data.json")));
        builder.Services.AddSingleton<JsonPerformanceDataRepository>();
        builder.Services.AddSingleton<IAppDiagnostics, MauiAppDiagnostics>();
        builder.Services.AddSingleton<IPerformanceDataRepository>(services => new DiagnosticPerformanceDataRepository(
            services.GetRequiredService<JsonPerformanceDataRepository>(), services.GetRequiredService<IAppDiagnostics>()));
        builder.Services.AddSingleton<ITrainingCatalog, DefaultTrainingCatalog>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<WorkoutStateService>();
        builder.Services.AddSingleton<IBackupFiles, MauiBackupFiles>();
        builder.Services.AddSingleton<IProgramFiles, MauiProgramFiles>();
        builder.Services.AddSingleton<IProgramPackageCodec, JsonProgramPackageCodec>();
        builder.Services.AddTransient<ImportWorkoutProgramsUseCase>();
        builder.Services.AddTransient<ExportWorkoutProgramsUseCase>();
        builder.Services.AddSingleton<IWorkoutFeedback, MauiWorkoutFeedback>();
        builder.Services.AddSingleton<IRestNotificationScheduler, AndroidRestNotificationScheduler>();
        builder.Services.AddSingleton<RestNotificationCoordinator>();
        builder.Services.AddSingleton<AppNavigationService>();
        builder.Services.AddTransient<MainPage>();
        builder.Services.AddSingleton<IReactLocalStorageAdapter, ReactLocalStorageMigrationAdapter>();
        builder.Services.AddTransient<LoadPerformanceDataUseCase>();
        builder.Services.AddTransient<SavePerformanceDataUseCase>();
        builder.Services.AddTransient<ImportPerformanceBackupUseCase>();
        builder.Services.AddTransient<ExportPerformanceBackupUseCase>();
#if DEBUG
        builder.Services.AddBlazorWebViewDeveloperTools();
        builder.Logging.AddDebug();
#endif

        return builder.Build();
    }
}

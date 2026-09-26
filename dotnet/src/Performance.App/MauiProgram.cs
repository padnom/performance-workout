using Microsoft.Extensions.Logging;
using Performance.Application;
using Performance.Infrastructure;

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
        builder.Services.AddSingleton<IPerformanceDataRepository, JsonPerformanceDataRepository>();
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

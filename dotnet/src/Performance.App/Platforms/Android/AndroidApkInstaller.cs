using Android.Content;
using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Storage;
using Performance.App.Services;

namespace Performance.App;

/// <summary>Opens the system package installer; Android keeps the data because the update uses the same signing key.</summary>
public sealed class AndroidApkInstaller : IApkInstaller
{
    private const string ApkMimeType = "application/vnd.android.package-archive";
    public string InstalledVersion => AppInfo.Current.VersionString;
    public string DownloadDirectory => FileSystem.CacheDirectory;
    public bool CanInstall => true;

    public Task<ApkInstallResult> InstallAsync(string apkPath)
    {
        var context = Platform.CurrentActivity ?? Android.App.Application.Context;
        try
        {
            if (OperatingSystem.IsAndroidVersionAtLeast(26) && context.PackageManager?.CanRequestPackageInstalls() != true)
            {
                var settings = new Intent(Android.Provider.Settings.ActionManageUnknownAppSources,
                    Android.Net.Uri.Parse($"package:{context.PackageName}"));
                settings.AddFlags(ActivityFlags.NewTask);
                context.StartActivity(settings);
                return Task.FromResult(ApkInstallResult.PermissionRequired);
            }
            // The MAUI Essentials provider (also used by Share) exposes the cache directory.
            var uri = AndroidX.Core.Content.FileProvider.GetUriForFile(context, $"{context.PackageName}.fileProvider", new Java.IO.File(apkPath));
            var install = new Intent(Intent.ActionView);
            install.SetDataAndType(uri, ApkMimeType);
            install.AddFlags(ActivityFlags.GrantReadUriPermission | ActivityFlags.NewTask);
            context.StartActivity(install);
            return Task.FromResult(ApkInstallResult.Started);
        }
        catch (Exception exception) when (exception is Java.Lang.Exception or ArgumentException)
        {
            IPlatformApplication.Current?.Services.GetService<IAppDiagnostics>()?.Record("update.installer", TimeSpan.Zero, exception);
            return Task.FromResult(ApkInstallResult.Unavailable);
        }
    }
}

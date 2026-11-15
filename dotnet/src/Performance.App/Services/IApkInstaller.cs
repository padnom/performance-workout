namespace Performance.App.Services;

public enum ApkInstallResult { Started, PermissionRequired, Unavailable }

/// <summary>Hands a downloaded APK to the Android package installer, which always asks the user to confirm.</summary>
public interface IApkInstaller
{
    string InstalledVersion { get; }
    string DownloadDirectory { get; }
    bool CanInstall { get; }
    Task<ApkInstallResult> InstallAsync(string apkPath);
}

using System.Net.Http.Headers;
using System.Security.Cryptography;
using Performance.Application;
using static Performance.App.Resources.Text;

namespace Performance.App.Services;

public enum AppUpdateStatus { Idle, Checking, UpToDate, Available, Downloading, PermissionRequired, Installing, Failed }

/// <summary>Checks the GitHub releases of the project and installs a newer signed APK after confirmation.</summary>
public sealed class AppUpdateService(HttpClient http, IApkInstaller installer, TimeProvider clock, IAppDiagnostics? diagnostics = null)
{
    public static readonly Uri LatestReleaseUri = new("https://api.github.com/repos/padnom/performance-workout/releases/latest");
    public static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);
    private readonly SemaphoreSlim _gate = new(1, 1);
    private DateTimeOffset? _lastCheck;
    private string? _downloadedPath;

    public AppUpdateStatus Status { get; private set; }
    public AppRelease? Available { get; private set; }
    public double Progress { get; private set; }
    public string Message { get; private set; } = "";
    public bool Dismissed { get; private set; }
    public string InstalledVersion => installer.InstalledVersion;
    public bool ShowBanner => Available is not null && !Dismissed;
    public bool Busy => Status is AppUpdateStatus.Checking or AppUpdateStatus.Downloading or AppUpdateStatus.Installing;
    public event Action? Changed;

    /// <summary>Silent automatic check, at most every <see cref="CheckInterval"/>; failures (offline) are ignored.</summary>
    public Task CheckInBackgroundAsync(CancellationToken cancellationToken = default)
    {
        if (_lastCheck is { } last && clock.GetUtcNow() - last < CheckInterval) return Task.CompletedTask;
        return CheckAsync(false, cancellationToken);
    }

    public async Task CheckAsync(bool userInitiated, CancellationToken cancellationToken = default)
    {
        if (!installer.CanInstall || !await _gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            if (Status is AppUpdateStatus.Downloading or AppUpdateStatus.Installing) return;
            Set(AppUpdateStatus.Checking, userInitiated ? T("Service.AppUpdateService.Checking") : Message);
            _lastCheck = clock.GetUtcNow();
            var release = await FetchLatestAsync(cancellationToken);
            if (release is not null && AppReleaseParser.IsNewer(release, installer.InstalledVersion))
            {
                if (Available?.Version != release.Version) { Dismissed = false; _downloadedPath = null; }
                Available = release;
                Set(AppUpdateStatus.Available, T("Service.AppUpdateService.Available", release.Version));
            }
            else
            {
                Available = null;
                Set(AppUpdateStatus.UpToDate, userInitiated ? T("Service.AppUpdateService.UpToDate", installer.InstalledVersion) : "");
            }
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException)
        {
            diagnostics?.Record("update.check", TimeSpan.Zero, userInitiated ? exception : null);
            Set(Available is null ? AppUpdateStatus.Idle : AppUpdateStatus.Available,
                userInitiated ? T("Service.AppUpdateService.CheckFailed") : Message);
        }
        finally { _gate.Release(); }
    }

    public void Dismiss() { Dismissed = true; Changed?.Invoke(); }

    public async Task InstallAsync(CancellationToken cancellationToken = default)
    {
        if (Available is not { } release || !await _gate.WaitAsync(0, cancellationToken)) return;
        try
        {
            Dismissed = false;
            if (_downloadedPath is null || !File.Exists(_downloadedPath))
                _downloadedPath = await DownloadAsync(release, cancellationToken);
            Set(AppUpdateStatus.Installing, T("Service.AppUpdateService.Installing"));
            var result = await installer.InstallAsync(_downloadedPath);
            if (result == ApkInstallResult.PermissionRequired)
                Set(AppUpdateStatus.PermissionRequired, T("Service.AppUpdateService.PermissionRequired"));
            else if (result == ApkInstallResult.Unavailable)
                Set(AppUpdateStatus.Failed, T("Service.AppUpdateService.InstallerFailed"));
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or IOException or InvalidDataException)
        {
            diagnostics?.Record("update.install", TimeSpan.Zero, exception);
            _downloadedPath = null;
            Set(AppUpdateStatus.Failed, exception is InvalidDataException
                ? T("Service.AppUpdateService.InvalidFile")
                : T("Service.AppUpdateService.DownloadInterrupted"));
        }
        finally { _gate.Release(); }
    }

    /// <summary>Called when the app comes back to the foreground, e.g. after granting the install permission.</summary>
    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (Status == AppUpdateStatus.PermissionRequired) return InstallAsync(cancellationToken);
        if (Status == AppUpdateStatus.Installing) Set(AppUpdateStatus.Available, Available is null ? "" : $"Version {Available.Version} disponible.");
        return CheckInBackgroundAsync(cancellationToken);
    }

    private async Task<AppRelease?> FetchLatestAsync(CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, LatestReleaseUri);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.Add(new ProductInfoHeaderValue("Performance-Android", installer.InstalledVersion));
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(15));
        using var response = await http.SendAsync(request, timeout.Token);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound) return null;
        response.EnsureSuccessStatusCode();
        var json = await response.Content.ReadAsStringAsync(timeout.Token);
        return json.Length > 1_000_000 ? null : AppReleaseParser.Parse(json);
    }

    private async Task<string> DownloadAsync(AppRelease release, CancellationToken cancellationToken)
    {
        Progress = 0;
        Set(AppUpdateStatus.Downloading, T("Service.AppUpdateService.Downloading", release.Version));
        var directory = Path.Combine(installer.DownloadDirectory, "updates");
        if (Directory.Exists(directory)) Directory.Delete(directory, true);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"performance-{release.Version}.apk");
        using var response = await http.GetAsync(release.ApkUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        await using var source = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using (var target = File.Create(path))
        {
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > AppReleaseParser.MaxApkBytes) throw new InvalidDataException("APK too large");
                hash.AppendData(buffer, 0, read);
                await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                var progress = Math.Min(1, (double)total / release.Size);
                if (progress - Progress >= 0.02) { Progress = progress; Changed?.Invoke(); }
            }
            if (total != release.Size) throw new InvalidDataException("Unexpected APK size");
            if (release.Sha256 is { } expected && Convert.ToHexStringLower(hash.GetHashAndReset()) != expected)
                throw new InvalidDataException("APK digest mismatch");
        }
        Progress = 1;
        return path;
    }

    private void Set(AppUpdateStatus status, string message)
    {
        Status = status;
        Message = message;
        Changed?.Invoke();
    }
}

using System.Net;
using System.Security.Cryptography;
using JetBrains.Annotations;
using Performance.App.Services;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Services;

[TestSubject(typeof(AppUpdateService))]
public sealed class AppUpdateServiceTests : IDisposable
{
    private static readonly byte[] Apk = Enumerable.Range(0, 300_000).Select(index => (byte)index).ToArray();
    private readonly Handler _handler = new();
    private readonly Installer _installer = new();
    private readonly Clock _clock = new();
    private readonly AppUpdateService _service;

    public AppUpdateServiceTests()
    {
        _installer.DownloadDirectory = Directory.CreateTempSubdirectory("performance-update-").FullName;
        _service = new(new HttpClient(_handler), _installer, _clock);
    }

    [Fact]
    public async Task Check_NewerRelease_ShowsBanner()
    {
        _handler.Release = Release("android-v1.4.0");

        await _service.CheckAsync(false);

        _service.Status.ShouldBe(AppUpdateStatus.Available);
        _service.Available!.Version.ShouldBe(new Version(1, 4, 0));
        _service.ShowBanner.ShouldBeTrue();
        _handler.Requests.Single().Headers.UserAgent.ToString().ShouldContain("Performance-Android");
    }

    [Fact]
    public async Task Check_SameVersion_IsUpToDateAndSilentUnlessRequested()
    {
        _handler.Release = Release("android-v1.3.0");

        await _service.CheckAsync(false);
        _service.Status.ShouldBe(AppUpdateStatus.UpToDate);
        _service.Message.ShouldBeEmpty();
        _service.ShowBanner.ShouldBeFalse();

        await _service.CheckAsync(true);
        _service.Message.ShouldContain("à jour");
    }

    [Fact]
    public async Task BackgroundCheck_Offline_IsSilentAndThrottled()
    {
        _handler.Fail = true;

        await _service.CheckInBackgroundAsync();
        await _service.CheckInBackgroundAsync();

        _service.Status.ShouldBe(AppUpdateStatus.Idle);
        _service.Message.ShouldBeEmpty();
        _handler.Requests.Count.ShouldBe(1);
        _clock.Now += AppUpdateService.CheckInterval;
        await _service.CheckInBackgroundAsync();
        _handler.Requests.Count.ShouldBe(2);
    }

    [Fact]
    public async Task UserCheck_Offline_ExplainsFailure()
    {
        _handler.Fail = true;
        await _service.CheckAsync(true);
        _service.Message.ShouldContain("connexion");
    }

    [Fact]
    public async Task Install_VerifiesDigestAndStartsInstaller()
    {
        _handler.Release = Release("android-v1.4.0");
        await _service.CheckAsync(false);

        await _service.InstallAsync();

        _installer.Installed.ShouldNotBeNull();
        File.ReadAllBytes(_installer.Installed).ShouldBe(Apk);
        _service.Status.ShouldBe(AppUpdateStatus.Installing);
    }

    [Fact]
    public async Task Install_CorruptedDownload_IsRejectedBeforeInstaller()
    {
        _handler.Release = Release("android-v1.4.0", digest: new string('0', 64));
        await _service.CheckAsync(false);

        await _service.InstallAsync();

        _installer.Installed.ShouldBeNull();
        _service.Status.ShouldBe(AppUpdateStatus.Failed);
        _service.Message.ShouldContain("invalide");
    }

    [Fact]
    public async Task Install_PermissionMissing_ResumesAfterReturningToApp()
    {
        _handler.Release = Release("android-v1.4.0");
        await _service.CheckAsync(false);
        _installer.Result = ApkInstallResult.PermissionRequired;

        await _service.InstallAsync();
        _service.Status.ShouldBe(AppUpdateStatus.PermissionRequired);
        _installer.Calls.ShouldBe(1);

        _installer.Result = ApkInstallResult.Started;
        await _service.ResumeAsync();

        _installer.Calls.ShouldBe(2);
        _handler.Downloads.ShouldBe(1);
        _service.Status.ShouldBe(AppUpdateStatus.Installing);
    }

    [Fact]
    public async Task Dismiss_HidesBannerUntilANewerVersionIsPublished()
    {
        _handler.Release = Release("android-v1.4.0");
        await _service.CheckAsync(false);
        _service.Dismiss();
        await _service.CheckAsync(false);
        _service.ShowBanner.ShouldBeFalse();

        _handler.Release = Release("android-v1.5.0");
        await _service.CheckAsync(false);
        _service.ShowBanner.ShouldBeTrue();
    }

    public void Dispose() => Directory.Delete(_installer.DownloadDirectory, true);

    private static string Release(string tag, string? digest = null) =>
        $$"""
        {"tag_name":"{{tag}}","body":"","assets":[{"name":"performance.apk","size":{{Apk.Length}},
         "digest":"sha256:{{digest ?? Convert.ToHexStringLower(SHA256.HashData(Apk))}}",
         "browser_download_url":"https://github.com/padnom/performance-workout/releases/download/{{tag}}/performance.apk"}]}
        """;

    private sealed class Handler : HttpMessageHandler
    {
        public string? Release { get; set; }
        public bool Fail { get; set; }
        public List<HttpRequestMessage> Requests { get; } = [];
        public int Downloads { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var download = request.RequestUri!.AbsolutePath.EndsWith(".apk");
            if (!download) Requests.Add(request);
            if (Fail) throw new HttpRequestException("offline");
            if (download)
            {
                Downloads++;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Apk) });
            }
            return Task.FromResult(Release is null ? new HttpResponseMessage(HttpStatusCode.NotFound)
                : new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Release) });
        }
    }

    private sealed class Installer : IApkInstaller
    {
        public string InstalledVersion => "1.3.0";
        public string DownloadDirectory { get; set; } = "";
        public bool CanInstall => true;
        public ApkInstallResult Result { get; set; } = ApkInstallResult.Started;
        public string? Installed { get; private set; }
        public int Calls { get; private set; }
        public Task<ApkInstallResult> InstallAsync(string apkPath)
        {
            Calls++;
            if (Result == ApkInstallResult.Started) Installed = apkPath;
            return Task.FromResult(Result);
        }
    }

    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = new(2026, 11, 15, 8, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

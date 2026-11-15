using JetBrains.Annotations;
using Shouldly;

namespace Performance.Application.UnitTests;

[TestSubject(typeof(AppReleaseParser))]
public sealed class AppReleaseParserTests
{
    private const string Digest = "sha256:ABCDEF0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [Fact]
    public void Parse_LatestGithubRelease_ReturnsVersionApkAndDigest()
    {
        var release = AppReleaseParser.Parse(Json("android-v1.4.0", ApkAsset()));

        release.ShouldNotBeNull();
        release.Version.ShouldBe(new Version(1, 4, 0));
        release.ApkUri.ShouldBe(new Uri("https://github.com/padnom/performance-workout/releases/download/android-v1.4.0/performance-1.4.0.apk"));
        release.Sha256.ShouldBe("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789");
        release.Size.ShouldBe(30_000_000);
        release.Notes.ShouldBe("Notes");
    }

    [Theory]
    [InlineData("{\"tag_name\":\"android-v1.4.0\",\"draft\":true,\"assets\":[ASSET]}")]
    [InlineData("{\"tag_name\":\"android-v1.4.0\",\"prerelease\":true,\"assets\":[ASSET]}")]
    [InlineData("{\"tag_name\":\"release-candidate\",\"assets\":[ASSET]}")]
    [InlineData("{\"tag_name\":\"android-v1.4.0\",\"assets\":[]}")]
    [InlineData("{\"tag_name\":\"android-v1.4.0\",\"assets\":[{\"name\":\"a.apk\",\"size\":10,\"browser_download_url\":\"http://github.com/a.apk\"}]}")]
    [InlineData("{\"tag_name\":\"android-v1.4.0\",\"assets\":[{\"name\":\"a.apk\",\"size\":10,\"browser_download_url\":\"https://evil.example/a.apk\"}]}")]
    [InlineData("{\"tag_name\":\"android-v1.4.0\",\"assets\":[{\"name\":\"a.apk\",\"size\":0,\"browser_download_url\":\"https://github.com/a.apk\"}]}")]
    [InlineData("not json")]
    [InlineData("[]")]
    public void Parse_UnusableRelease_ReturnsNull(string json) =>
        AppReleaseParser.Parse(json.Replace("ASSET", ApkAsset())).ShouldBeNull();

    [Fact]
    public void Parse_IgnoresNonApkAssetsAndInvalidDigest()
    {
        var notes = "{\"name\":\"notes.txt\",\"size\":5,\"browser_download_url\":\"https://github.com/notes.txt\"}";
        var release = AppReleaseParser.Parse(Json("v2.0", notes + "," + ApkAsset("sha256:short")));

        release.ShouldNotBeNull();
        release.Version.ShouldBe(new Version(2, 0, 0));
        release.ApkUri.AbsolutePath.ShouldEndWith(".apk");
        release.Sha256.ShouldBeNull();
    }

    [Theory]
    [InlineData("1.4.0", "1.3.0", true)]
    [InlineData("1.3.1", "1.3", true)]
    [InlineData("1.3.0", "1.3.0", false)]
    [InlineData("1.3.0", "1.3", false)]
    [InlineData("1.2.9", "1.3.0", false)]
    [InlineData("2.0.0", "1.10.0", true)]
    public void IsNewer_ComparesNumericVersions(string published, string installed, bool expected)
    {
        var release = new AppRelease(AppReleaseParser.ParseVersion(published)!, new Uri("https://github.com/a.apk"), null, 1, "");
        AppReleaseParser.IsNewer(release, installed).ShouldBe(expected);
    }

    private static string ApkAsset(string digest = Digest) =>
        $"{{\"name\":\"performance-1.4.0.apk\",\"size\":30000000,\"digest\":\"{digest}\",\"browser_download_url\":\"https://github.com/padnom/performance-workout/releases/download/android-v1.4.0/performance-1.4.0.apk\"}}";

    private static string Json(string tag, string assets) =>
        $"{{\"tag_name\":\"{tag}\",\"draft\":false,\"prerelease\":false,\"body\":\"Notes\",\"assets\":[{assets}]}}";
}

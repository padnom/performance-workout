using JetBrains.Annotations;
using Performance.App.Services;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Services;

[TestSubject(typeof(ShareFileName))]
public sealed class ShareFileNameTests
{
    private static readonly DateTime Moment = new(2026, 10, 17, 8, 30, 0, DateTimeKind.Utc);

    [Fact]
    [Trait("AC", "AC-40")]
    public void ForSession_WithAccentsAndSymbols_ShouldProduceCleanSlug()
    {
        // Act
        var result = ShareFileName.ForSession("Séance libre · 17/10", Moment);

        // Assert
        result.ShouldBe("performance-seance-seance-libre-17-10-2026-10-17-083000.json");
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void ForSession_WithPathTraversal_ShouldContainNoSeparatorNorDotDot()
    {
        // Act
        var result = ShareFileName.ForSession("../../etc/passwd", Moment);

        // Assert
        result.ShouldNotContain("/");
        result.ShouldNotContain("\\");
        result.ShouldNotContain("..");
        result.ShouldBe("performance-seance-etc-passwd-2026-10-17-083000.json");
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void ForSession_WithLongName_ShouldTruncateSlugToFortyCharacters()
    {
        // Act
        var result = ShareFileName.ForSession(new string('a', 100), Moment);

        // Assert
        result.ShouldBe($"performance-seance-{new string('a', 40)}-2026-10-17-083000.json");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("···///")]
    [Trait("AC", "AC-40")]
    public void ForSession_WithEmptyOrSymbolsOnly_ShouldFallBackToDefaultSlug(string name)
    {
        // Act
        var result = ShareFileName.ForSession(name, Moment);

        // Assert
        result.ShouldBe("performance-seance-seance-2026-10-17-083000.json");
    }

    [Fact]
    [Trait("AC", "AC-40")]
    public void ForSession_WithTruncationLeavingTrailingDash_ShouldTrimDash()
    {
        // Act
        var result = ShareFileName.ForSession(new string('a', 39) + " bbb", Moment);

        // Assert
        result.ShouldBe($"performance-seance-{new string('a', 39)}-2026-10-17-083000.json");
    }
}

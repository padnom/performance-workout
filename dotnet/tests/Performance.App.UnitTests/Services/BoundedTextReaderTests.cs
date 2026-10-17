using System.Text;
using JetBrains.Annotations;
using Performance.App.Services;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Services;

[TestSubject(typeof(BoundedTextReader))]
public sealed class BoundedTextReaderTests
{
    [Fact]
    [Trait("AC", "AC-47")]
    public async Task ReadAsync_WithContentAtExactLimit_ShouldReturnContent()
    {
        // Arrange
        var content = new string('a', 20_000);
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));

        // Act
        var result = await BoundedTextReader.ReadAsync(stream, 20_000);

        // Assert
        result.ShouldBe(content);
    }

    [Fact]
    [Trait("AC", "AC-47")]
    public async Task ReadAsync_WithContentOverLimit_ShouldThrowFormatException()
    {
        // Arrange
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(new string('a', 20_001)));

        // Act
        var exception = await Should.ThrowAsync<FormatException>(() => BoundedTextReader.ReadAsync(stream, 20_000));

        // Assert
        exception.Message.ShouldBe("Ce fichier est trop volumineux.");
    }

    [Fact]
    [Trait("AC", "AC-47")]
    public async Task ReadAsync_WithEmptyStream_ShouldReturnEmptyString()
    {
        // Arrange
        using var stream = new MemoryStream();

        // Act
        var result = await BoundedTextReader.ReadAsync(stream, 10);

        // Assert
        result.ShouldBe(string.Empty);
    }
}

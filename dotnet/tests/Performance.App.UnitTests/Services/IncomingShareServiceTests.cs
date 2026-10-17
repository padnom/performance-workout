using JetBrains.Annotations;
using Performance.App.Services;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Services;

[TestSubject(typeof(IncomingShareService))]
public sealed class IncomingShareServiceTests
{
    [Fact]
    [Trait("AC", "AC-47")]
    public void Consume_AfterOffer_ShouldReturnShareOnce()
    {
        // Arrange
        var service = new IncomingShareService();
        service.Offer(new IncomingShare("{}", null));

        // Act
        var first = service.Consume();
        var second = service.Consume();

        // Assert
        first.ShouldBe(new IncomingShare("{}", null));
        second.ShouldBeNull();
        service.HasPending.ShouldBeFalse();
    }

    [Fact]
    [Trait("AC", "AC-47")]
    public void Offer_WithSubscriber_ShouldRaiseReceivedAndKeepPending()
    {
        // Arrange
        var service = new IncomingShareService();
        var raised = 0;
        service.Received += () => raised++;

        // Act
        service.Offer(new IncomingShare(null, "Impossible de lire ce fichier."));

        // Assert
        raised.ShouldBe(1);
        service.HasPending.ShouldBeTrue();
    }

    [Fact]
    [Trait("AC", "AC-47")]
    public void Offer_WhenShareAlreadyPending_ShouldReplaceIt()
    {
        // Arrange
        var service = new IncomingShareService();
        service.Offer(new IncomingShare("first", null));

        // Act
        service.Offer(new IncomingShare("second", null));

        // Assert
        service.Consume()!.Json.ShouldBe("second");
        service.Consume().ShouldBeNull();
    }

    [Fact]
    [Trait("AC", "AC-47")]
    public void Offer_WhenSubscriberReadsState_ShouldRaiseOutsideLock()
    {
        // Arrange
        var service = new IncomingShareService();
        IncomingShare? seen = null;
        service.Received += () => seen = Task.Run(service.Consume).GetAwaiter().GetResult();

        // Act
        service.Offer(new IncomingShare("x", null));

        // Assert
        seen.ShouldBe(new IncomingShare("x", null));
    }
}

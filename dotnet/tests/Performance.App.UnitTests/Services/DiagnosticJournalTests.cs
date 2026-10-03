using JetBrains.Annotations;
using System.Text.Json;
using Performance.App.Services;
using Shouldly;
using Xunit;

namespace Performance.App.UnitTests.Services;

[TestSubject(typeof(DiagnosticJournal))]
public sealed class DiagnosticJournalTests
{
    [Fact]
    public void Diagnostics_BoundsHistoryAndExcludesExceptionMessages()
    {
        var journal = new DiagnosticJournal(TimeProvider.System, 2);
        journal.Record("data.load", TimeSpan.Zero);
        journal.Record("data.save", TimeSpan.FromMilliseconds(8), new IOException("private workout values"));
        journal.Record("rest.complete", TimeSpan.FromMilliseconds(1));
        var entries = journal.Snapshot();
        entries.Count.ShouldBe(2);
        entries[0].ErrorType.ShouldBe(nameof(IOException));
        JsonSerializer.Serialize(entries).ShouldNotContain("private workout values");
        entries[0].Milliseconds.ShouldBe(8);
    }
}

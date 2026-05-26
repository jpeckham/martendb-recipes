using MartenDbRecipes.Contracts;

namespace MartenDbRecipes.Tests;

public class RecipeCatalogTests
{
    [Fact]
    public void Catalog_covers_the_requested_marten_solution_areas()
    {
        var keys = MartenRecipeCatalog.All.Select(x => x.Key).ToArray();

        Assert.Contains("documents", keys);
        Assert.Contains("event-sourcing", keys);
        Assert.Contains("cqrs", keys);
        Assert.Contains("pub-sub", keys);
    }

    [Fact]
    public void Ticket_summary_replays_events_into_current_read_model()
    {
        var ticketId = Guid.NewGuid();
        var opened = new TicketOpened(ticketId, "Stale projection", "Event daemon is behind", DateTimeOffset.UtcNow);
        var assigned = new TicketAssigned("operations");
        var noted = new TicketNoteAdded("Restarted the worker.");
        var closed = new TicketClosed("Projection caught up.");

        var summary = TicketSummary.FromOpened(opened);
        summary.RecordAssignment(assigned);
        summary.RecordNote(noted);
        summary.RecordClosure(closed);

        Assert.Equal(ticketId, summary.Id);
        Assert.Equal("Stale projection", summary.Title);
        Assert.Equal("operations", summary.AssignedTeam);
        Assert.Equal("Closed", summary.Status);
        Assert.Equal(1, summary.NoteCount);
        Assert.Equal("Projection caught up.", summary.Resolution);
    }
}

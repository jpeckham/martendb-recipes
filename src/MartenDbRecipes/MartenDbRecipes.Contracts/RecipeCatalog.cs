namespace MartenDbRecipes.Contracts;

public sealed record MartenRecipeInfo(
    string Key,
    string Title,
    string Solves,
    string MartenFeature,
    string TryIt);

public static class MartenRecipeCatalog
{
    public static IReadOnlyList<MartenRecipeInfo> All { get; } =
    [
        new(
            "documents",
            "Document DB",
            "Stores rich JSON documents in PostgreSQL while keeping .NET object querying and ACID writes.",
            "IDocumentSession.Store, LINQ queries, JSONB-backed documents",
            "Create and update knowledge articles without designing tables first."),
        new(
            "event-sourcing",
            "Event Sourcing and Aggregates",
            "Captures every business change as an append-only event stream and rebuilds aggregate state from history.",
            "Events.StartStream, Events.Append, FetchStreamAsync",
            "Open a support ticket, assign it, add notes, and close it."),
        new(
            "cqrs",
            "CQRS Read Models",
            "Keeps write-side events separate from query-optimized read models.",
            "Inline single-stream snapshot projection",
            "Query ticket summaries without replaying events per request."),
        new(
            "pub-sub",
            "Pub/Sub Style Subscriptions",
            "Runs background subscribers over committed event pages for integration messages or local side effects.",
            "Marten event subscriptions and async daemon",
            "Inspect integration messages created from ticket events.")
    ];
}

public sealed record KnowledgeArticle(
    Guid Id,
    string Title,
    string Category,
    string Body,
    DateTimeOffset UpdatedAt);

public sealed record UpsertArticleRequest(string Title, string Category, string Body);

public sealed record OpenTicketRequest(string Title, string Description);

public sealed record AssignTicketRequest(string Team);

public sealed record AddTicketNoteRequest(string Note);

public sealed record CloseTicketRequest(string Resolution);

public sealed record TicketOpened(Guid TicketId, string Title, string Description, DateTimeOffset OpenedAt);

public sealed record TicketAssigned(string Team);

public sealed record TicketNoteAdded(string Note);

public sealed record TicketClosed(string Resolution);

public sealed class TicketSummary
{
    public Guid Id { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string Status { get; set; } = "Open";

    public string AssignedTeam { get; set; } = "unassigned";

    public int NoteCount { get; set; }

    public string? Resolution { get; set; }

    public DateTimeOffset OpenedAt { get; set; }

    public static TicketSummary FromOpened(TicketOpened opened) =>
        new()
        {
            Id = opened.TicketId,
            Title = opened.Title,
            Description = opened.Description,
            OpenedAt = opened.OpenedAt,
            Status = "Open"
        };

    public void RecordAssignment(TicketAssigned assigned) => AssignedTeam = assigned.Team;

    public void RecordNote(TicketNoteAdded _) => NoteCount++;

    public void RecordClosure(TicketClosed closed)
    {
        Status = "Closed";
        Resolution = closed.Resolution;
    }
}

public sealed class IntegrationMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid StreamId { get; set; }

    public long Sequence { get; set; }

    public string EventType { get; set; } = string.Empty;

    public string Summary { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

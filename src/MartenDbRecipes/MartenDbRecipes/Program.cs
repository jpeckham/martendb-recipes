using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using JasperFx.Events.Subscriptions;
using JasperFx;
using JasperFx.Events;
using Marten;
using Marten.Events.Aggregation;
using Marten.Events;
using Marten.Subscriptions;
using Microsoft.AspNetCore.Components;
using MartenDbRecipes.Components;
using MartenDbRecipes.Contracts;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveWebAssemblyComponents();
builder.Services.AddScoped(sp =>
{
    var navigation = sp.GetRequiredService<NavigationManager>();
    return new HttpClient { BaseAddress = new Uri(navigation.BaseUri) };
});

var connectionString = builder.Configuration.GetConnectionString("Marten")
    ?? "Host=localhost;Port=5432;Database=martendb_recipes;Username=postgres;Password=postgres";

builder.Services.AddMarten(options =>
    {
        options.Connection(connectionString);
        options.AutoCreateSchemaObjects = AutoCreate.CreateOrUpdate;
        options.Projections.AddGlobalProjection(new TicketSummaryProjection(), ProjectionLifecycle.Inline);
    })
    .AddSubscriptionWithServices<IntegrationMessageSubscription>(ServiceLifetime.Singleton, options =>
    {
        options.Name = "integration-message-outbox";
        options.IncludeType<TicketOpened>();
        options.IncludeType<TicketAssigned>();
        options.IncludeType<TicketNoteAdded>();
        options.IncludeType<TicketClosed>();
        options.Options.SubscribeFromPresent();
    })
    .AddAsyncDaemon(DaemonMode.Solo);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseWebAssemblyDebugging();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveWebAssemblyRenderMode()
    .AddAdditionalAssemblies(typeof(MartenDbRecipes.Client._Imports).Assembly);

app.MapMartenRecipeApi();

app.Run();

static class MartenRecipeApi
{
    public static void MapMartenRecipeApi(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api");

        group.MapGet("/recipes", () => MartenRecipeCatalog.All);

        group.MapGet("/articles", async (IQuerySession session, CancellationToken ct) =>
            await session.Query<KnowledgeArticle>()
                .OrderByDescending(x => x.UpdatedAt)
                .ToListAsync(ct));

        group.MapPost("/articles", async (UpsertArticleRequest request, IDocumentSession session, CancellationToken ct) =>
        {
            var article = new KnowledgeArticle(
                Guid.NewGuid(),
                request.Title,
                request.Category,
                request.Body,
                DateTimeOffset.UtcNow);

            session.Store(article);
            await session.SaveChangesAsync(ct);

            return Results.Created($"/api/articles/{article.Id}", article);
        });

        group.MapPut("/articles/{id:guid}", async (Guid id, UpsertArticleRequest request, IDocumentSession session, CancellationToken ct) =>
        {
            var existing = await session.LoadAsync<KnowledgeArticle>(id, ct);
            if (existing is null)
            {
                return Results.NotFound();
            }

            var article = existing with
            {
                Title = request.Title,
                Category = request.Category,
                Body = request.Body,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            session.Store(article);
            await session.SaveChangesAsync(ct);

            return Results.Ok(article);
        });

        group.MapPost("/tickets", async (OpenTicketRequest request, IDocumentSession session, CancellationToken ct) =>
        {
            var ticketId = Guid.NewGuid();
            session.Events.StartStream<TicketSummary>(
                ticketId,
                new TicketOpened(ticketId, request.Title, request.Description, DateTimeOffset.UtcNow));

            await session.SaveChangesAsync(ct);

            return Results.Created($"/api/tickets/{ticketId}", new { TicketId = ticketId });
        });

        group.MapPost("/tickets/{id:guid}/assign", async (Guid id, AssignTicketRequest request, IDocumentSession session, CancellationToken ct) =>
        {
            session.Events.Append(id, new TicketAssigned(request.Team));
            await session.SaveChangesAsync(ct);
            return Results.Accepted($"/api/tickets/{id}");
        });

        group.MapPost("/tickets/{id:guid}/notes", async (Guid id, AddTicketNoteRequest request, IDocumentSession session, CancellationToken ct) =>
        {
            session.Events.Append(id, new TicketNoteAdded(request.Note));
            await session.SaveChangesAsync(ct);
            return Results.Accepted($"/api/tickets/{id}");
        });

        group.MapPost("/tickets/{id:guid}/close", async (Guid id, CloseTicketRequest request, IDocumentSession session, CancellationToken ct) =>
        {
            session.Events.Append(id, new TicketClosed(request.Resolution));
            await session.SaveChangesAsync(ct);
            return Results.Accepted($"/api/tickets/{id}");
        });

        group.MapGet("/tickets/{id:guid}/live", async (Guid id, IQuerySession session, CancellationToken ct) =>
        {
            var events = await session.Events.FetchStreamAsync(id, token: ct);
            var summary = ProjectTicket(id, events);
            return summary is null ? Results.NotFound() : Results.Ok(summary);
        });

        group.MapGet("/ticket-summaries", async (IQuerySession session, CancellationToken ct) =>
            await session.Query<TicketSummary>()
                .OrderByDescending(x => x.OpenedAt)
                .ToListAsync(ct));

        group.MapGet("/integration-messages", async (IQuerySession session, CancellationToken ct) =>
            await session.Query<IntegrationMessage>()
                .OrderByDescending(x => x.CreatedAt)
                .Take(50)
                .ToListAsync(ct));
    }

    private static TicketSummary? ProjectTicket(Guid id, IReadOnlyList<IEvent> events)
    {
        TicketSummary? summary = null;

        foreach (var @event in events)
        {
            summary = TicketSummaryProjection.Apply(summary, id, @event.Data);
        }

        return summary;
    }
}

public sealed class IntegrationMessageSubscription : SubscriptionBase
{
    public override Task<IChangeListener> ProcessEventsAsync(
        EventRange page,
        ISubscriptionController controller,
        IDocumentOperations operations,
        CancellationToken cancellationToken)
    {
        foreach (var @event in page.Events)
        {
            operations.Store(new IntegrationMessage
            {
                StreamId = @event.StreamId,
                Sequence = @event.Sequence,
                EventType = @event.EventTypeName,
                Summary = Describe(@event.Data)
            });
        }

        return Task.FromResult<IChangeListener>(NoChangeListener.Instance);
    }

    private static string Describe(object data) =>
        data switch
        {
            TicketOpened opened => $"Ticket opened: {opened.Title}",
            TicketAssigned assigned => $"Ticket assigned to {assigned.Team}",
            TicketNoteAdded => "Ticket note added",
            TicketClosed closed => $"Ticket closed: {closed.Resolution}",
            _ => data.GetType().Name
        };

    private sealed class NoChangeListener : IChangeListener
    {
        public static readonly NoChangeListener Instance = new();

        public Task BeforeCommitAsync(IDocumentSession session, Marten.Services.IChangeSet commit, CancellationToken token) =>
            Task.CompletedTask;

        public Task AfterCommitAsync(IDocumentSession session, Marten.Services.IChangeSet commit, CancellationToken token) =>
            Task.CompletedTask;
    }
}

public sealed class TicketSummaryProjection : SingleStreamProjection<TicketSummary, Guid>
{
    public override TicketSummary Evolve(TicketSummary? snapshot, Guid id, IEvent e)
    {
        return Apply(snapshot, id, e.Data) ?? new TicketSummary { Id = id };
    }

    public static TicketSummary? Apply(TicketSummary? snapshot, Guid id, object data)
    {
        if (data is TicketOpened opened)
        {
            return TicketSummary.FromOpened(opened);
        }

        if (snapshot is null)
        {
            return new TicketSummary { Id = id };
        }

        switch (data)
        {
            case TicketAssigned assigned:
                snapshot.RecordAssignment(assigned);
                break;
            case TicketNoteAdded noted:
                snapshot.RecordNote(noted);
                break;
            case TicketClosed closed:
                snapshot.RecordClosure(closed);
                break;
        }

        return snapshot;
    }
}

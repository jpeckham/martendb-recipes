# Marten DB Recipes

A .NET 10 recipe app for Marten on PostgreSQL. The server is an ASP.NET Core minimal API, the UI is a hosted Blazor WebAssembly client, and Docker Compose starts the app plus PostgreSQL.

## Run

```powershell
docker compose up --build
```

Open `http://localhost:8080`.

For local development without containerizing the app:

```powershell
docker compose up postgres
dotnet run --project src/MartenDbRecipes/MartenDbRecipes/MartenDbRecipes.csproj
```

## What The Recipes Cover

- Document DB: `KnowledgeArticle` is stored and queried as a Marten JSON document.
- Event sourcing/aggregates: support tickets are written as `TicketOpened`, `TicketAssigned`, `TicketNoteAdded`, and `TicketClosed` events.
- CQRS: `TicketSummary` is an inline Marten snapshot projection optimized for reads.
- Pub/sub style processing: `IntegrationMessageSubscription` consumes committed ticket events through Marten's async daemon and writes integration-message documents.

## API Surface

- `GET /api/recipes`
- `GET /api/articles`
- `POST /api/articles`
- `PUT /api/articles/{id}`
- `POST /api/tickets`
- `POST /api/tickets/{id}/assign`
- `POST /api/tickets/{id}/notes`
- `POST /api/tickets/{id}/close`
- `GET /api/tickets/{id}/live`
- `GET /api/ticket-summaries`
- `GET /api/integration-messages`

## Verify

```powershell
dotnet test MartenDbRecipes.sln
```

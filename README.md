# ChatBoard API

Backend for a personal kanban board that tracks which claude.ai chats still need action
from me. Single user, self-hosted, no auth. The Vue 3 UI is a separate task; this repo is
the API and schema.

## Stack

- .NET 9 minimal API, EF Core 9 + SQLite
- xUnit + EF Core InMemory for tests

## Run

```bash
dotnet run --project src/ChatBoard.Api
# SQLite file defaults to board.db; override with ConnectionStrings__Board
```

The schema is created at startup via `EnsureCreated()`. Once it settles, switch to
`dotnet ef migrations` (see the marked spot in `Program.cs`).

## Test

```bash
dotnet test
```

## The board

Four statuses, on the axis of *who is blocking* rather than progress:

| Status | Meaning |
|---|---|
| `NeedsAction` | Ball is in my court |
| `Waiting` | Blocked on someone/something else |
| `Reference` | Keep findable, requires nothing |
| `Closed` | Loop closed |

Cards carry a `double` position (sort key) rather than a contiguous index, so a
drag-and-drop writes one row. When neighbours converge past a threshold, the target
column is renumbered inside the move's transaction. All ordering arithmetic lives in
`BoardPosition`.

## Endpoints

| Method | Route | Notes |
|---|---|---|
| `GET` | `/api/board` | Non-closed columns, cards ordered by position. `?includeClosed=true` adds Closed |
| `POST` | `/api/cards` | `{ title, url?, chatUuid?, status?, nextAction?, tags? }`, defaults to `NeedsAction` |
| `GET` | `/api/cards/{id}` | |
| `PUT` | `/api/cards/{id}` | `{ title, url?, nextAction?, tags }` — details only, not status |
| `PATCH` | `/api/cards/{id}/move` | `{ status, aboveId?, belowId? }` — status change and reorder are one operation |
| `DELETE` | `/api/cards/{id}` | Hard delete |
| `POST` | `/api/import/conversations` | Upload the export; upserts staging rows, creates no cards |
| `GET` | `/api/import/pending` | Staged chats not yet promoted, newest first |
| `POST` | `/api/import/{chatUuid}/promote` | Creates a `NeedsAction` card from a staged chat (idempotent) |

Statuses travel the wire as their names (`"NeedsAction"`).

## Import

There is no public API for listing claude.ai conversations. The only official route to
chat history is **Settings → Account → Export Data**, which emails a ZIP. Extract it and
upload the `conversations.json` (raw body or multipart `file` field) to
`POST /api/import/conversations`.

`ConversationExportReader` is tolerant of format drift: it sniffs the first non-whitespace
character — `[` means a JSON array, anything else means JSONL (one conversation per line).
Records missing optional fields still import; malformed records are skipped and counted.

Import **stages**, it does not populate the board. Most chats need nothing, so a card per
conversation would flood the board — I promote the ones I care about from
`GET /api/import/pending`.

# Notes API

![CI](https://github.com/AdrianStan/NotesApi/actions/workflows/ci.yml/badge.svg)

REST backend for a note-taking service shared by small teams. Users belong to one or more teams and can create, search, edit and delete their teams' notes.

Built with ASP.NET Core (.NET 10), EF Core and SQLite.

## Run it

Requirements: .NET 10 SDK.

```
dotnet run --project NotesApi
```

- The SQLite database (`notes.db`) is created and seeded on first run. No setup needed.
- API: http://localhost:5297
- Swagger UI: http://localhost:5297/swagger
- Health check: http://localhost:5297/health
- `NotesApi/NotesApi.http` has ready-made requests for every endpoint and error case. Open it in Visual Studio, VS Code (REST Client) or Rider and send them one by one.

## Run the tests

```
dotnet test
```

10 integration tests (xUnit and WebApplicationFactory) start the API in memory against a temporary SQLite database and call it over HTTP. GitHub Actions builds the project and runs the tests on every push.

## Identifying the caller

Authentication is out of scope for this exercise. Each request identifies the user with a header:

```
X-User-Id: 1
```

Seeded data:

| User | Id | Teams |
|---|---|---|
| Alice Johnson | 1 | Development, Operations |
| Bob Smith | 2 | Development |
| Carol Lee | 3 | Operations, Testing |

Teams: Development (1), Operations (2), Testing (3). One note is seeded in Development.

## Endpoints

| Method | Route | Description | Success | Errors |
|---|---|---|---|---|
| GET | `/api/teams` | Teams the caller belongs to | 200 | 401 |
| GET | `/api/teams/{teamId}/notes?search=&page=&pageSize=` | A team's notes, newest first, searchable, paged (max 100) | 200 | 401, 403, 404 |
| POST | `/api/teams/{teamId}/notes` | Create a note | 201 | 400, 401, 403, 404 |
| GET | `/api/notes/{id}` | One note | 200 | 401, 403, 404 |
| PUT | `/api/notes/{id}` | Update title and content. The body must include the note's current `version` | 200 | 400, 401, 403, 404, 409 |
| DELETE | `/api/notes/{id}` | Delete a note | 204 | 401, 403, 404 |
| GET | `/health` | Health check | 200 | |

Errors use the standard ProblemDetails format.

## Design choices

### 1. Data model: users can belong to several teams

Four tables: Users, Teams, TeamMembers and Notes. People often work with more than one team (for example Development and Operations), so users and teams are many-to-many through the TeamMembers join table. Its key is (TeamId, UserId), so a user cannot be added to the same team twice.

Notes belong to a team, not to a user, because they are shared. AuthorId records who wrote each note. Deleting a note sets `IsActive = false` (soft delete), so an accidental delete can be recovered, and a global query filter hides inactive notes from every query. Deleting a team or user is restricted, so notes are never removed silently.

Tradeoff: one team per user (a TeamId column on Users) would be simpler, but it would not match how teams actually work.

### 2. Access control behind an interface

All access checks go through `ICurrentUser`. It reads the caller from the `X-User-Id` header and checks team membership:

- missing or unknown user: 401
- user is not in the team: 403
- team does not exist: 404

The header is a deliberate shortcut. In production the user would come from SSO (for example Okta or Entra ID) as a JWT validated with `AddJwtBearer`, and team membership could become a policy-based authorization rule. Controllers and the service depend only on `ICurrentUser`, so that change means writing one new implementation of the interface, with no changes to the endpoints.

### 3. Two people editing the same note

Notes are shared, so two teammates can edit the same note at the same time. Without a check, the last save silently overwrites the other person's work.

Each note has a `Version`. An update must send the version it was based on:

- If it does not match the current version, the API returns 409 and the client should reload the note.
- EF Core also uses `Version` as a concurrency token, so if two saves arrive at the same moment, only one succeeds and the other gets 409.

This is optimistic concurrency. Locking a note while someone edits it (pessimistic locking) would block teammates and needs lock cleanup, which is too heavy for notes.

## Other decisions

- **SQLite and EF Core.** SQLite needs no server, so the project runs on any OS with no setup. EF Core fits a CRUD workload, provides migrations and the concurrency token, and makes moving to SQL Server or PostgreSQL a provider change. Dapper would pay off for complex, performance-critical queries, which this API does not have.
- **Database created on first run.** Migrations are applied at startup, and database files are excluded from Git.
- **Service layer.** Business rules live in `NotesService` behind `INotesService`, and controllers only map HTTP to the service. Rules stay in one place and are easy to test. This is also the interface and service pattern I use daily in production .NET applications.
- **DTOs.** Requests accept only what the caller is allowed to set (title, content, version). Author, team, dates and `IsActive` are set by the API, so they cannot be faked. Responses never expose the database entities.
- **Centralized error handling.** One exception handler maps not found, forbidden, conflict and unauthorized errors to status codes in ProblemDetails format. Unexpected errors return a generic 500 and are logged with full details on the server only. The alternative is a `Result<T>` pattern instead of exceptions; for an API this size, exceptions keep the code simpler.
- **CI and health check.** GitHub Actions builds and tests every push on a clean Linux machine, which also proves the project runs outside my own environment. `/health` lets a load balancer or monitoring tool check that the app is up.

## With more time

- Real authentication (JWT from an identity provider) and roles, for example only the author or a team admin can delete a note.
- Endpoints to manage teams and members (currently seeded).
- Return 404 instead of 403 for notes in teams the caller does not belong to, so note ids cannot be probed.
- Full-text search (SQLite FTS5 or SQL Server full-text) instead of `LIKE`, which scans every note in a team.
- Cursor-based paging for very large teams.
- Note history: keep previous versions instead of overwriting them.
- Rate limiting, and a database check in the health endpoint.
- Centralized structured logging (Serilog or NLog to Seq or Application Insights) with a correlation id per request.
- Unit tests for `NotesService` alongside the integration tests.
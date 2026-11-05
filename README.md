# RaceDay

**Portfolio of Evidence — PROG6212 (Programming 2B)**
Michael-John Uren · ST10465421

RaceDay is a full-stack event management platform for the South African road
running, walking and cycling community. Organizers create and manage events,
categories and results; participants browse and enter events, track their
personal performance history, and prepare for race day using live weather
and route information. The finished platform will be a containerized,
cloud-aware, API-driven **ASP.NET Core** application.

## Progress

### Part 1 — Database design (complete)

A relational database design for the platform, produced before any
application or API code:

| Deliverable | File |
|---|---|
| Entity relationship diagram + schema notes | [`database/RaceDay-ERD.png`](database/RaceDay-ERD.png) |
| SQL Server schema + seed data script | [`database/RaceDay-Schema.sql`](database/RaceDay-Schema.sql) |
| RESTful API endpoint specification | [`documents/API-specification.docx`](documents/API-specification.docx) |


The schema is a normalized (3NF), 10-table design (`Roles`, `Clubs`,
`Users`, `UserRoles`, `Venues`, `Routes`, `Events`, `Categories`,
`Entries`, `Results`) covering organizers, events, categories, entries and
results. Live weather is fetched from an external API rather than stored.

To create and seed the database: open `database/RaceDay-Schema.sql` in SQL
Server Management Studio while connected to a SQL Server instance, then
press F5 to run the whole script. It (re)creates a `RaceDayDB` database with
all tables, constraints and representative sample data.

### Part 2 — ASP.NET Core Web API (complete)

A RESTful **ASP.NET Core Web API (.NET 10)** backed by **SQL Server** through
**Entity Framework Core 10 (Code-First)**, with JWT authentication, server-side
session management, role-based access control, Swagger and an xUnit test suite
that runs on every push through GitHub Actions.

#### Functionality

| Area | Organizer | Participant | Anonymous |
|---|---|---|---|
| Register / log in / refresh / log out | ✔ | ✔ | register & log in |
| View & update own profile | ✔ | ✔ | |
| Create, update, publish, delete own events | ✔ | | |
| View events | ✔ (incl. own drafts) | ✔ | ✔ (published) |
| Define age / distance categories | ✔ (own events) | | |
| View categories | ✔ | ✔ | ✔ |
| Enter an event by choosing a category | | ✔ | |
| View all entries (roster) for own events | ✔ | | |
| Capture / correct finish times and positions | ✔ (own events) | | |
| View own results & performance history | | ✔ | |
| Clubs, venues, routes | create venues/routes | view | view |

Admins (seeded account only) can manage any event, clubs, venue deletion and grant roles.
Every endpoint in [`documents/API-specification.docx`](documents/API-specification.docx) is implemented
except the two live-weather endpoints, which are out of scope for this part.

#### Authentication and sessions

* `POST /api/auth/register` — personal details plus `role`: `Organizer` (or `Organiser`) or `Participant`.
  Admin cannot be self-assigned.
* Passwords are hashed with ASP.NET Core Identity's `PasswordHasher` (PBKDF2-HMAC-SHA512, 100 000
  iterations, per-password salt). Plain-text passwords are never stored.
* `POST /api/auth/login` creates a row in `UserSessions` and returns a short-lived JWT access token
  (30 min) bound to that session, plus a refresh token (7 days, stored only as a SHA-256 hash).
* `POST /api/auth/refresh-token` rotates the refresh token; `POST /api/auth/logout` revokes the session.
* Every authenticated request re-checks the session, so a logged-out, expired or deactivated session's
  access token is rejected immediately (401) even before the JWT itself expires.
* A global fallback policy requires authentication everywhere except endpoints explicitly marked
  anonymous; organizer/participant/admin rules are enforced with role-based `[Authorize]` attributes
  and ownership checks (an organizer can only manage their own events).

#### Database (Code-First)

The entity configurations in `src/RaceDay.Api/Data/Configurations` reproduce
`database/RaceDay-Schema.sql`: table and column names, `nvarchar` lengths, `date` / `time(0)` /
`time(3)` / `datetime2` / `decimal(p,s)` types, named `PK_` / `FK_` / `UQ_` / `CHK_` / `DF_`
constraints, the same `ON DELETE` behaviours and the same `IX_` indexes. The script's seed rows are
applied with `HasData`, using identical identity values. The migrations in
`src/RaceDay.Api/Migrations` were generated with `dotnet ef`: `InitialCreate` (the ten scripted
tables) and `AddUserSessions`.

Two deliberate notes:

1. **`UserSessions`** is the only table added beyond the Part 1 script. It is required for session
   management (refresh tokens and logout).
2. **Event distance.** The Part 1 schema stores distance on each *category* (and route), not on the
   event, because one race day usually offers several distances. The API therefore returns an event's
   distances as `distancesKm` (one value per category) rather than adding a column that would break
   parity with the script. An event's location is its venue; its type is the `discipline`
   (`Running`, `Walking`, `Cycling`, `Multisport` — `run`, `walk` and `cycle` are accepted).

The routes table has no owner column, so an organizer may edit or delete a route unless it is used by
another organizer's event.

#### Running locally (Visual Studio 2026 / .NET 10 SDK)

1. Open `RaceDay.sln`.
2. The connection string in `src/RaceDay.Api/appsettings.json` points at
   `(localdb)\MSSQLLocalDB`, database `RaceDayDB`. Change it if you use another SQL Server instance.
3. Create the database from the migrations, either in the Package Manager Console:
   ```
   Update-Database -Project RaceDay.Api -StartupProject RaceDay.Api
   ```
   or from a terminal:
   ```
   dotnet tool install --global dotnet-ef
   dotnet ef database update --project src/RaceDay.Api
   ```
   (In the Development environment the API also applies pending migrations on startup.)
4. Press **F5**. The browser opens Swagger UI at `/swagger`.
5. Call `POST /api/auth/login`, copy `accessToken`, click **Authorize** and paste it.

Seeded accounts all use the demo password **`RaceDay@2026`** (development only):

| Email | Role |
|---|---|
| `admin@raceday.co.za` | Admin |
| `sarah.vdm@raceday.co.za` | Organizer (event 1) |
| `thabo.nkosi@raceday.co.za` | Organizer (event 2) |
| `lindiwe.d@example.co.za` | Participant |
| `johan.botha@example.co.za` | Participant |
| `aisha.patel@example.co.za` | Organizer + Participant |

The JWT signing key in `appsettings.json` is a development placeholder. Override `Jwt__SigningKey`
(environment variable or user secrets) outside development.

#### Tests

`tests/RaceDay.Tests` (xUnit) hosts the real API pipeline in memory with `WebApplicationFactory`
and an isolated EF Core in-memory database per test class, so authentication, session validation
and authorization run exactly as in production. Coverage includes:

* registration and login — success and failure (duplicate email, invalid role, weak password,
  wrong password, unknown email, deactivated account, refresh and logout);
* every protected endpoint returning 401 without a token;
* role enforcement — participants rejected from organizer features, organizers rejected from
  participant features, non-admins rejected from admin features, organizers rejected from other
  organizers' events;
* event management — create, update, publish, delete/cancel and validation errors;
* categories, enrolments (the stored participant–event–category link, duplicates, capacity, age and
  gender rules, closed registration), results, profiles, reference data and the Swagger document;
* unit tests for password hashing, JWT creation and discipline mapping.

```
dotnet test RaceDay.sln
```

#### CI/CD

`.github/workflows/workflow.yml` runs on every push to any branch, on pull requests into `main`,
and on demand. It restores and builds the solution, fails if the EF Core migrations have drifted
from the entity model (`dotnet ef migrations has-pending-model-changes`), runs the full test suite
(159 tests at the end of Part 2) and uploads the results as a build artifact.

Watch the [RaceDay project walkthrough on YouTube](https://youtu.be/LIaOX_tK4VQ).

Recent GitHub Actions workflow runs:

![GitHub Actions workflow runs](images/github-actions-workflow-runs.png)

### Part 3 — ASP.NET Core MVC web app, Azure Blob Storage and Docker (this branch: `part-3`)

Part 3 adds **RaceDay.Web**, an ASP.NET Core MVC front end for organizers and participants. It has
no database and no business rules of its own: every page reads and writes through the Part 2 API.
Event banner images are stored in **Azure Blob Storage** behind the API, the web app ships as a
**Docker** image, and CI now builds the web app and validates its Dockerfile.

#### Architecture

```
 Browser ──(cookies, HTML forms)──▶ RaceDay.Web (MVC)  ──(HTTPS + JSON, Bearer JWT)──▶ RaceDay.Api ──▶ SQL Server
    │                                 cookie auth ticket                                   │
    │                                 + server-side Session                                └──▶ Azure Blob Storage
    │                                   (access/refresh tokens)                                  container: event-banners
    └────────────────────────(banner <img> loaded straight from the blob URL)──────────────────────┘
```

* **Server-to-server API calls.** A typed `HttpClient` (`Services/Api/RaceDayApiClient`) wraps every
  endpoint the UI needs. A delegating handler (`BearerTokenHandler`) attaches the user's access token,
  refreshes it through `POST /api/auth/refresh-token` shortly before it expires or when the API answers
  401, and stores the rotated refresh token back in the session. If refreshing fails, the user is
  signed out and sent to the login page.
* **Errors from the API** (ProblemDetails and validation problems) come back as `ApiResult<T>`. Field
  errors are attached to the matching form fields; everything else is shown through one shared partial
  (`_StatusMessages`).
* **Auth state.** Logging in calls `POST /api/auth/login`. The tokens stay server-side in ASP.NET Core
  Session, so the browser only ever holds an HttpOnly session cookie and an HttpOnly auth cookie. The
  auth cookie carries the user's id, name and roles (taken from the API's `user.roles`). Registering
  calls `POST /api/auth/register` and then logs straight in. Logging out revokes the API session
  (`POST /api/auth/logout`), clears the session and the cookie, and redirects to Login.
* **Authorization.** A fallback policy requires a signed-in user everywhere except Home, the public
  event calendar, Account pages, static files and `/healthz`. The `Organizer` area requires the
  Organizer or Admin role; the `Participant` area requires Participant. All POSTs are protected by
  anti-forgery tokens.

#### Pages by role

| Role | Page | API endpoints used |
|---|---|---|
| Anyone | Home, event calendar (search, discipline, province, date range), event detail | `GET /api/events`, `GET /api/events/{id}` |
| Anyone | Register, log in | `POST /api/auth/register`, `POST /api/auth/login` |
| Organizer | Dashboard: summary cards plus a table of events with enrolments and dates | `GET /api/events/dashboard` |
| Organizer | My events: create and edit (name, description, date, start time, entry window, venue, event type, first distance, banner), publish, delete | `POST/PUT/DELETE /api/events…`, `POST /api/events/{id}/publish`, `POST /api/events/{id}/banner`, `GET /api/venues` |
| Organizer | Categories: add, edit and remove distances and age groups | `POST /api/events/{id}/categories`, `PUT/DELETE /api/categories/{id}` |
| Organizer | Enrolments: roster per event with category, status and payment | `GET /api/events/{id}/entries` |
| Organizer | Results: grid of time, status and positions per participant, then **Publish results** (from race day onwards) | `POST /api/entries/{id}/result`, `PUT /api/results/{id}`, `POST /api/events/{id}/complete` |
| Participant | Find races: filtered upcoming events as banner cards | `GET /api/events` |
| Participant | Event detail and enrolment: choose a category; API rule failures (closed, full, age, gender, duplicate) are shown on the form | `POST /api/categories/{id}/entries` |
| Participant | My enrolments, with withdraw | `GET /api/users/me/entries`, `PUT /api/entries/{id}/cancel` |
| Participant | My results: times and overall, category and gender positions | `GET /api/users/me/performance-history` |
| Signed in | Profile: view and edit | `GET/PUT /api/users/me`, `GET /api/clubs` |

Admins can open the organizer area and manage any event. They cannot create events (the API only
lets organizers do that), so the UI hides **New event** for them. A user with both roles (for example
`aisha.patel@example.co.za`) sees both sets of links.

#### API additions

| Endpoint | Purpose |
|---|---|
| `POST /api/events/{id}/banner` | `multipart/form-data` upload (field `file`; JPEG, PNG or WebP, ≤ 5 MB). Owner or admin only. Stores the image in Blob Storage, sets `bannerImageUrl` and deletes the previous banner. Returns 503 if storage isn't configured or can't be reached. |
| `POST /api/events/{id}/complete` | Publishes results: a Published event whose date has arrived becomes Completed. |
| `GET /api/events/dashboard` | One row per event the organizer owns (admins: all), with category, active-enrolment and result counts and an `isUpcoming` flag, from a single query. |

`EventSummaryResponse` now includes `bannerImageUrl`, so list cards can show banners. No schema
changes were needed, and no new migration. 25 new tests cover the three endpoints, bringing the
suite to 184. In the tests, blob storage is replaced by an in-memory fake.

#### Banner storage (Azure Blob)

1. The organizer chooses an image on the event form. The web app saves the event, then posts the file
   to `POST /api/events/{id}/banner`.
2. The API checks the type and size, then writes the image to container `event-banners` as
   `events/{eventId}/{guid}.{ext}`. The container is created on first use with anonymous *blob* read
   access, so the browser can load images straight from the blob URL.
3. The blob URL is saved on the event and returned to every client.

Configuration (API): `Storage:ConnectionString` and `Storage:BannerContainer` (default `event-banners`).
Never commit the connection string. Set it with user secrets or an environment variable:

```
dotnet user-secrets set "Storage:ConnectionString" "<connection string>" --project src/RaceDay.Api
# or
Storage__ConnectionString="<connection string>"
```

For local development, `appsettings.Development.json` uses `UseDevelopmentStorage=true`, which points
at the **Azurite** emulator:

```
docker run -d -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0
```

On a real storage account, *Allow Blob anonymous access* must be enabled for the account. Otherwise
the container is created private and banners will not display.

#### Running the web app locally

1. Start the API as described above (Part 2). It listens on `https://localhost:7245`.
2. Start `RaceDay.Web` (in Visual Studio, set multiple startup projects, or run
   `dotnet run --project src/RaceDay.Web`). It listens on `https://localhost:7180`.
3. `Api:BaseUrl` in `src/RaceDay.Web/appsettings.json` defaults to `https://localhost:7245`.
   Override it with the `Api__BaseUrl` environment variable.

#### Docker

The Dockerfile at `src/RaceDay.Web/Dockerfile` is a multi-stage build: `sdk:10.0` restores (the
project file is copied first, so the package layer is cached) and publishes, then `aspnet:10.0` runs
the app as the image's non-root user on port 8080, with a `HEALTHCHECK` on `/healthz`. Build from the
repository root so `global.json` is used:

```
docker build -f src/RaceDay.Web/Dockerfile -t raceday-web .
docker run -p 8080:8080 -e Api__BaseUrl=https://<api-host> raceday-web
```

* The container serves plain HTTP. Put TLS on the reverse proxy or ingress in front of it.
* `Api__BaseUrl` must be reachable *from inside the container*. To use an API running on your
  machine, use `http://host.docker.internal:5245` (the API's `http` profile). The API's HTTPS
  development certificate is not trusted inside the container.
* The API, not the web container, talks to Azure. Give the API `Storage__ConnectionString`.
* ASP.NET Core Data Protection keys live inside the container, so users must log in again after the
  container is recreated. Mount a volume at `/home/app/.aspnet/DataProtection-Keys` to keep them.

#### CI/CD (Part 3)

The `build-and-test` job builds the whole solution, including the MVC app (it also has an explicit
`dotnet build src/RaceDay.Web` step). It then checks for EF Core migration drift and runs all 184
tests. A second job, `docker-build`, builds the web app's Docker image to validate the Dockerfile.
It does not push the image.

## Repository structure

```
database/                  SQL Server schema + seed data script & entity relationship diagram
documents/                 RESTful API endpoint specification
src/RaceDay.Api/           ASP.NET Core Web API (+ Services/ for JWT, passwords and Azure Blob banners)
  Controllers/             Auth, Users, Clubs, Venues, Routes, Events, Categories, Entries, Results
  Data/                    DbContext, Fluent API configurations, seed data
  Dtos/                    Request and response models
  Migrations/              EF Core Code-First migrations
  Models/                  Entities
  Services/                Password hashing and JWT token services
src/RaceDay.Web/           ASP.NET Core MVC web app (talks to the API only)
  Areas/Organizer/         Dashboard, events, categories, enrolments, results
  Areas/Participant/       Find races, event detail & enrolment, my enrolments, my results
  Controllers/             Home, public events, account (login/register/logout), profile
  Services/Api/            Typed API client, bearer/refresh handler, session token store
  Dockerfile               Multi-stage container build
tests/RaceDay.Tests/       xUnit integration and unit tests
.github/workflows/         GitHub Actions build, test and Docker build pipeline
```

## AI Usage Disclosure

- I used Claude to assist with the creation of the database schema. The AI provided suggestions for table structures, relationships, which I then reviewed and modified to fit the specific requirements of the RaceDay platform. It was also used to generate the database seed data, and formatting the README file.
- Part 2: I used Claude to help scaffold the Web API, the EF Core entity configurations,the controllers, the unit tests and the GitHub Actions workflow, which I then reviewed and tested.

## Tech stack

- ASP.NET Core Web API and ASP.NET Core MVC (.NET 10), Bootstrap 5
- Entity Framework Core 10 (Code-First) with SQL Server
- JWT bearer authentication with server-side sessions
- Swagger / OpenAPI (Swashbuckle)
- xUnit, WebApplicationFactory, EF Core InMemory
- Azure Blob Storage (Azure.Storage.Blobs), Azurite for local development
- Docker
- GitHub Actions

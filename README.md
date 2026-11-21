# RaceDay
A full-stack web-based event management system for the South African road running, walking, and cycling community. It replaces the paper-based registration and spreadsheet tracking that many local road events still rely on with a single system for
organising events and taking part in them.

This repository holds Part 1 and Part 2 of my three-part project for Programming 2B (PROG6212) in Diploma in Software Development at Rosebank Intenational.
Silindokuhle Gqukani
ST10461013

## Part 1 deliverables (`/docs`)
The documents are in `docs/`: the ERD (`ERD.png`), the API plan (`API plan - *.md`) and the SQL script (`schema.sql`).

File descriptions
1.Raceday_erd.png which is the Entity Relationship Diagram, containing 6 entities (Users,
Events, Routes, Categories, Enrolments, Results) with
primary keys, foreign keys, and cardinality

2.Raceday_api_endpoint_plan.md which is the Full REST API endpoint specification (method, route,
description, role required, request body, expected
response)
3.raceday_database.sql which is SQL Server script that creates the full schema with
constraints, plus seed data

### Database design
The data model centres on Users (holding both roles via a Role column), who create
Events . Each Event has one or more Categories (the individual races within it, e.g.
“10km Fun Run”) and Routes (the course details). The participants join a Category through
Enrolments , and each Enrolment can produce one Results record once the race is run.

### Part 1 CI/CD
A GitHub Actions workflow validates that the repository structure is correct — specifically,
that the /docs folder exists and contains the ERD, API plan, and SQL script.
Build status: 

### Walkthrough video

---

## Part 2 - RESTful API Development (`RaceDay.Api`, `RaceDay.Tests`)
An ASP.NET Core 8 Web API for the RaceDay system. It implements the endpoint plan and schema from Part 1
(`docs/API plan - *.md`, `docs/schema.sql`) using Entity Framework Core (code-first) on SQL Server, with
session-based authentication for the Organiser and Participant roles, Swagger, xUnit tests and a GitHub Actions workflow.

### Solution layout

```
RaceDay.sln
├── RaceDay.Api/                 ASP.NET Core Web API
│   ├── Auth/                    Session keys + [SessionAuthorize] role filter
│   ├── Controllers/             Auth, Users, Events (incl. weather), Routes, Categories, Enrolments, Results
│   ├── Data/                    RaceDayDbContext (EF Core Fluent API, mirrors schema.sql)
│   ├── Database/                part2-schema-additions.sql (see "Schema" below)
│   ├── Dtos/                    Request/response models with validation
│   ├── Models/                  Entities and enums
│   ├── Services/                PBKDF2 password hasher, weather service, database initialiser
│   └── Program.cs               Services, session, Swagger, pipeline
├── RaceDay.Tests/               xUnit integration + unit tests
└── .github/workflows/dotnet.yml GitHub Actions CI (restore, build, test)
```

### Running it
Requirements: .NET 8 SDK and SQL Server (LocalDB ships with Visual Studio).

1. Open `RaceDay.sln` in Visual Studio and set `RaceDay.Api` as the startup project.
2. Check the connection string in `RaceDay.Api/appsettings.json` (defaults to LocalDB, database `RaceDayDB`).
3. Press F5. Swagger opens at `/swagger`.

#### Schema
The entity classes use the exact table and column names, lengths, unique constraints, CHECK constraints and
non-cascading foreign keys from `docs/schema.sql` (a test checks this). Two columns are **new in Part 2** because the
Part 2 brief says every event must capture a distance and an event type, which the Part 1 ERD did not have:

| Table | New column | Definition |
|---|---|---|
| Events | `EventType` | `VARCHAR(20) NOT NULL`, CHECK in (`Run`, `Walk`, `Cycle`) |
| Events | `DistanceKM` | `DECIMAL(6,2) NOT NULL` |


#### Trying it in Swagger

1. `POST /api/auth/register` as a Participant (or as an Organiser with the access code).
2. `POST /api/auth/login`. The browser stores the session cookie automatically.
3. Every other endpoint can now be tried with "Try it out". Log in as the other role to see the 403 responses.

### Endpoints (all from the Part 1 plan)

| Area | Method and route | Role |
|---|---|---|
| Auth | `POST /api/auth/register`, `POST /api/auth/login` | Public |
| | `POST /api/auth/logout` *(added)* | Logged in |
| Users | `GET /api/users/me`, `PUT /api/users/me` | Organiser or Participant |
| Events | `GET /api/events`, `GET /api/events/{eventId}` | Public |
| | `POST /api/events`, `PUT /api/events/{eventId}`, `DELETE /api/events/{eventId}` | Organiser (owner) |
| Routes | `GET /api/events/{eventId}/routes` | Public |
| | `POST /api/events/{eventId}/routes`, `PUT /api/routes/{routeId}`, `DELETE /api/routes/{routeId}` | Organiser (owner) |
| Categories | `GET /api/events/{eventId}/categories` | Public |
| | `POST /api/events/{eventId}/categories`, `PUT /api/categories/{categoryId}`, `DELETE /api/categories/{categoryId}` | Organiser (owner) |
| Enrolments | `POST /api/categories/{categoryId}/enrolments`, `GET /api/enrolments/me`, `DELETE /api/enrolments/{enrolmentId}` | Participant |
| | `GET /api/events/{eventId}/enrolments` | Organiser (owner) |
| Results | `POST /api/enrolments/{enrolmentId}/result`, `PUT /api/results/{resultId}` | Organiser (owner) |
| | `GET /api/events/{eventId}/results` | Public |
| | `GET /api/results/me` | Participant |
| Weather | `GET /api/events/{eventId}/weather` | Organiser or Participant |

Small additions beyond the plan: `GET /api/routes/{routeId}` and `GET /api/categories/{categoryId}` (used for the
`201 Created` location headers) and the logout endpoint. `GET /api/events` lists upcoming events by default, as the plan
says; add `includePast=true` to see all. Its filters are `location`, `date`, `type`, `search` and `mine`.

#### Where the implementation differs from the Part 1 plan 

| Plan | Implementation | Reason |
|---|---|---|
| Login "issues a JWT" and returns `{ token, user }` | Login starts a **server-side session** and returns the user | The Part 2 brief requires session management. The browser keeps an HttpOnly session cookie. |
| Event body: `eventName, eventDate, location, description, weatherLocationCode` | Also requires `eventType` and `distanceKM` | Part 2 says each event must capture a type (run, walk, cycle) and a distance. |
| Register body has `role` | Organisers also send `organiserAccessCode` | See "Managing the Organiser role" below. |
| Profile update body: `firstName, lastName, phoneNumber` | Same. Email and role cannot be changed | Email is the login identity. |
| Categories are the races within an event | A category's **name** carries the label, for example "Under 20", "Senior", "10km", "21km" | Keeps the Part 1 schema (`CategoryName`, `DistanceKM`, `MaxParticipants`, `EntryFee`) unchanged. |

### Design decisions

**Managing the Organiser role.** If anyone could pick "Organiser" when registering, any Participant could grant
themselves the power to create, edit and delete events. Registration therefore takes a role, but registering as an
Organiser also needs an *organiser access code* (`Auth:OrganiserAccessCode`, compared in constant time).
Participants register freely. In production, keep the code out of source control:

**Session authentication.** Login verifies the password, clears any earlier session (preventing session fixation) and
stores the user id and role in the server-side session. `[SessionAuthorize]` returns **401** when nobody is logged in
and **403** when the logged-in role is not allowed. The role is always read from the session, never from the request.
Public endpoints (browsing events, categories, routes and the leaderboard) have no attribute.

**Password storage.** Passwords are hashed with PBKDF2 (HMAC-SHA256, 210,000 iterations, unique 16-byte salt) and
compared in constant time. The stored value (about 85 characters) fits `Users.PasswordHash VARCHAR(255)`. Plain-text
passwords are never stored, logged or returned.

**Ownership.** Being an Organiser is not enough: an Organiser can only change events, routes, categories, enrolment
lists and results that belong to *their own* events (403 otherwise).

**Business rules.**
* Enrolling: the event must not have taken place and the category must have space (`MaxParticipants`). A participant
  can enter a category once (the schema has a unique constraint on participant and category) but may enter more than
  one category of the same event. Each enrolment is `Confirmed` and gets a bib number built from the event's initials,
  for example `CTC-10001`.
* Cancelling sets `Status = 'Cancelled'`: history is kept and the place is freed. Re-enrolling in the same category
  reactivates that row (and its bib number), because the schema allows one row per participant and category.
  Once a result exists the enrolment can no longer be cancelled, and a cancelled enrolment cannot receive a result.
* Results: one per enrolment, and a position is unique within a category. `Finished` needs a finish time and a position;
  `DNF` and `DSQ` have no position.
* A category cannot be deleted while it has enrolments, and its capacity cannot be lowered below the number enrolled.
* Every foreign key is non-cascading, as in your script, so deleting an event removes its routes, categories,
  enrolments and results explicitly in code.

**Weather.** `GET /api/events/{eventId}/weather` uses the event's `WeatherLocationCode` (for example `ZA-CT`), maps it to
coordinates in `appsettings.json` (`Weather:Locations`, which you can extend) and calls the free Open-Meteo API (no key
needed). Forecasts are available up to 15 days ahead; for later dates the current conditions are returned. A code can also
be raw coordinates such as `-33.92,18.42`.

### Tests
The tests host the real API in memory (`WebApplicationFactory`) and replace SQL Server with an isolated EF Core in-memory
database and the live weather service with a fake, so they need no external infrastructure and run identically on a
developer machine and on GitHub Actions. Every test creates its own users with unique emails.

| File | What it covers |
|---|---|
| `AuthenticationTests` | Registration (success, duplicate, weak password, bad email, missing/unknown role, organiser code), login (valid, wrong password, unknown user), logout, public vs non-public access, profile view/update, password hashing |
| `EventManagementTests` | Organiser create/update/delete of events, categories and routes, validation failures, ownership (403), filters, viewing by both roles, weather (success, missing code, unknown code, outage) |
| `RoleAccessTests` | Participants rejected from Organiser functionality and vice versa, anonymous callers (401), role changes on re-login |
| `EnrolmentAndResultTests` | Enrolment success and correct recording of participant + category + event, bib numbers, capacity, duplicates, cancelling and re-enrolling, organiser view, result capture and viewing, leaderboard, DNF, corrections and every failure rule |
| `SwaggerTests` | Every method and route from the Part 1 plan appears in Swagger with a description and responses |
| `SchemaConformanceTests` | The EF model matches `docs/schema.sql`: tables, columns, lengths, unique constraints, non-cascading keys |

## Part 3 - ASP.NET Core MVC application
The `RaceDay.Mvc` project is the browser-facing application. It consumes the API over HTTP; it does **not** reference the API's EF Core DbContext or access SQL Server directly. Authentication is performed by the API. The MVC app keeps the API's HttpOnly session cookie in its own server-side session and forwards that cookie on subsequent API calls.

### Part 3 features
- Public upcoming-event catalogue with name/type filters and event detail pages.
- Registration and login through the Part 2 API, API session-cookie forwarding, role-specific navigation, logout and protected role pages.
- Organiser dashboard with event counts and enrolment summaries; event create/edit/delete; banner upload; category create/update/delete; participant enrolment list; result capture and publication.
- Participant event discovery, category enrolment, personal enrolment list, published race history and profile editing/profile picture upload.
- Enrolment status badges, responsive Bootstrap layout, shared RaceDay colours and consistent navigation.
- MVC validation and friendly API error messages rather than exposing raw exception details.

### Azure Blob Storage (all uploads go through the API)
The MVC project sends multipart image uploads to the Part 2 API. The API validates PNG/JPEG/WebP files (maximum 5 MB), stores them in Azure Blob Storage, and persists the resulting URL against the Event or User. The MVC application never connects to Azure directly.

### CI/CD
GitHub Actions restores and builds the full solution (including `RaceDay.Mvc`), runs the API test suite, then builds both Docker images. 

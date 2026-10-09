# Web CRM Project Plan (Rev 2, .NET)

Oct 4, 2026 · @Kostas

## Summary and stack decision

Build it with **C# / ASP.NET Core (.NET 10 LTS) + Blazor (Interactive Server) + SQL Server + EF Core**. Total learning build: about 247 hours for the MVP plus contingency (225 planned, plus about 22 for features added on 8 Oct), then a 25 to 30 hour post-MVP Phase 9, roughly 5 months at about 15 hours a week with no fixed end date, worked in evenings and weekends, with zero spend on tools.

**Revision 2 (4 Oct 2026):** this copy replaces Django + PostgreSQL + HTMX with the .NET stack. The original plan stays unchanged as Rev 1. Scope, user stories, permissions and operations carry over; tools, phases, architecture and risks are rewritten.

**Revision 2.1 (8 Oct 2026):** hosting moves to MonsterASP (Windows/IIS with SQL Server in the same datacenter) for staging and demo; Docker/Linux becomes an optional learning item. MVP scope gains API tokens, company settings and branding, notifications, live updates, saved views and favourites, dark mode, a Ctrl+K palette and a public demo mode; Products and Quotes become post-MVP Phase 9; invoices and orders stay out. Performance target tightened to 300 ms. Details in the page-by-page spec.

**Progress:** Phase 0 under way. Toolchain installed on 7 Oct (Visual Studio 2026 Community, SQL Server 2025 Developer, SSMS, GitHub account kenmex). BookPicker built as the Phase 0 practice app and deployed on MonsterASP at nextread.runasp.net.

Why this stack for an Access developer:

- **Closest model to Access forms.** Blazor components have markup plus event handlers (`OnClick`, `ValueChanged`), the same mental model as a form with VBA behind it, written in C#.
- **Reuses what you know.** SQL Server, T-SQL, SSRS and Power BI stay in the stack instead of being replaced.
- **Matches the target market.** Clients replacing Access or Excel/VBA are mostly Microsoft shops on SQL Server, Azure, Entra ID and IIS, and their "to web" job posts ask for C#/.NET or Power Apps.
- **Batteries included.** Identity, authorization policies, EF Core migrations, anti-forgery and parameterised SQL ship with the framework.

Why not the alternatives:

- **Django + PostgreSQL (Rev 1)** is excellent for learning, but rarely requested in Access/VBA replacement work and drops your SQL Server and SSRS strengths.
- **React / Next.js** has the most job posts overall, but it is a crowded general market, doubles the surface area (API + front end), and gives you no edge over younger developers.
- **Power Apps** is the main low-code competitor for Access replacement and worth learning next (see roadmap), but it hides the code you are trying to learn and costs clients premium licences for SQL Server.

## Objectives and success criteria

The project succeeds when you can build, deploy and run a multi-user web CRM without following a tutorial. The finished CRM is the evidence, not the goal.

| # | Objective | Measured by |
| --- | --- | --- |
| 1 | Build a Blazor app from an empty solution | A new CRUD module (e.g. Products) added in under 4 hours, no tutorial |
| 2 | Model data the EF Core way | ERD implemented as entities; migrations replayable from zero on a fresh database |
| 3 | Secure a multi-user app | Permission matrix enforced in UI and API via authorization policies; Sentry shows no unhandled errors for 2 weeks |
| 4 | Expose and consume an API | ASP.NET Core Web API with OpenAPI, used by Power BI or Power Automate |
| 5 | Deploy to Windows/IIS hosting | App runs on MonsterASP (IIS) with WebSockets and always-on verified; optional: the same build in a Linux container |
| 6 | Deploy and operate a public app | crm.mexdb.com (or the runasp.net address until the domain is mapped) live on HTTPS; a restore from backup completed in under 30 minutes |
| 7 | Work in a professional workflow | All work tracked in GitHub Issues; every change through a pull request with CI green |
| 8 | Write tests that catch regressions | Unit tests on every service method; bUnit on key components; Playwright covers the 5 core flows |

## CRM MVP scope

The MVP covers the core of most CRMs: companies, contacts, a sales pipeline, activities and a dashboard. Unchanged from Rev 1.

**In scope (MVP)**

- Accounts (companies) with addresses, industry, owner, status
- Contacts linked to accounts (one primary account, optional many-to-many later)
- Leads, with a convert-to-account/contact/opportunity action
- Opportunities with stages, value, probability, expected close date; kanban pipeline view
- Activities: calls, meetings, tasks, with due dates, owner and completion; a "my tasks today" view
- Notes and file attachments on any record
- Global search across accounts, contacts, opportunities
- Users, teams, roles (Admin, Manager, Sales), record ownership
- Audit trail (who changed what, when)
- Excel import (accounts, contacts) and export of any list
- Dashboard: pipeline by stage, won/lost this month, overdue tasks

Added 8 Oct 2026 (see the page-by-page spec):

- API tokens page for Power BI, Power Automate and integrations
- Company settings and branding (logo, colours, defaults) applied to the theme
- In-app notifications, pushed live
- Live pipeline board and live record changes
- Saved list views, recently viewed and favourite records
- Dark mode, Ctrl+K command palette, keyboard shortcuts
- Public demo mode: sign in as a demo role, data reset nightly

**Out of scope (later)**

- Email sync (Outlook/Gmail), mass email campaigns
- Products and quotes (post-MVP Phase 9, see the spec); invoices and orders stay out entirely, accepted quotes hand off to the client's accounting system (QuickBooks / Xero API later)
- Custom fields (design for it with a JSON column or EAV table, but don't build the UI yet)
- Native app (responsive web + PWA cover it; .NET MAUI Blazor Hybrid is the post-MVP option, reusing the same components)

## Zero-cost toolchain

Every tool below is free and open source, or free for a solo developer. Several popular .NET libraries changed to paid licences recently (EPPlus, FluentAssertions, MediatR, AutoMapper), so check the licence of anything you add. Verify hosting free tiers before relying on them; Oracle Cloud and Azure both need a card at signup, so a misconfigured resource can bill.

| Purpose | Tool | Notes |
| --- | --- | --- |
| Language / runtime | C# 14, .NET 10 LTS | LTS, supported to November 2028 |
| Web framework | ASP.NET Core, Blazor Web App template | Interactive Server render mode for the app; static server rendering for login pages |
| UI components | MudBlazor | Data grid, dialogs, forms, kanban-friendly drag and drop; MIT licence |
| Database | SQL Server 2025 Developer Edition (local) | Free, full features, not licensed for production |
| Production database | MonsterASP SQL Server (same datacenter as the app); SQL Server Express or Azure SQL free offer as alternatives | Express is free in production; size cap per database, verify for your version |
| Data access | EF Core 10 (code-first) | Migrations replace hand-run DDL; raw SQL or Dapper only when a query needs it |
| DB tools | SSMS + Visual Studio SQL Server Object Explorer | You already know SSMS |
| IDE | Visual Studio Community | Free for individuals; closest to the VBA editor experience; VS Code + C# Dev Kit as the light alternative |
| Auth | ASP.NET Core Identity, then Microsoft.Identity.Web | Local accounts + 2FA first, Entra ID sign-in in Phase 5 |
| API | ASP.NET Core Web API + built-in OpenAPI + Scalar UI | Feeds Power BI, Power Automate, a future front end |
| Background jobs | Hangfire (SQL Server storage) | Free core edition (LGPL); built-in dashboard; Quartz.NET is the alternative |
| Excel import / export | ClosedXML | MIT; avoid EPPlus (commercial licence since v5) |
| PDF reports | QuestPDF, or SSRS | QuestPDF Community licence is free under $1M revenue; SSRS reuses your skills |
| Charts | MudBlazor charts or ApexCharts.Blazor | Dashboard tiles; Power BI can read SQL Server directly |
| Logging | Serilog | Structured logs to console and file |
| Error tracking | Sentry (free tier) | Exceptions with stack traces and user context |
| Uptime monitoring | UptimeRobot (free tier) | Alerts when the site stops responding; 5-minute pings also keep the free tier from sleeping |
| Unit tests | xUnit + Shouldly | Avoid FluentAssertions v8+ (commercial) |
| Component tests | bUnit | Tests Blazor components without a browser |
| Integration tests | Testcontainers + SQL Server container | Real database per test run, local and in CI |
| End-to-end tests | Playwright for .NET | Clicks through the real UI |
| Formatting / analysis | `dotnet format`, .NET analyzers, `.editorconfig` | Warnings as errors in CI |
| Version control | Git + GitHub Free | Unlimited private repos |
| Project tracking | GitHub Issues + Projects | Backlog, kanban board and milestones next to the code |
| CI | GitHub Actions | Build, test against a SQL Server service container, publish image |
| Dependency and security checks | Dependabot, `dotnet list package --vulnerable`, GitHub code scanning | Weekly update PRs and vulnerability alerts |
| Containers | Docker Desktop or WSL2 + Docker Engine | Desktop is free for individuals and small businesses; WSL2 route is free regardless |
| Email | Free transactional SMTP tier (e.g. Brevo) + MailKit | Password reset, task reminders; needs SPF/DKIM on mexdb.com |
| Backup storage | Backblaze B2 free tier + `rclone` | Off-server copies of database backups and attachments |
| Wireframes / ERD | Excalidraw or Penpot; dbdiagram.io or draw.io | Sketch the 6 key screens; ERD and data dictionary |
| Hosting | MonsterASP (free tier to start) | Windows/IIS, .NET 10, SQL Server, free Let's Encrypt; verify always-on, domain mapping and database size cap |
| Caching | HybridCache (built into .NET) | Lookups, settings and dashboard KPIs |
| Telemetry | OpenTelemetry + dotnet-counters | Request timings and circuit counts against the performance budgets |

Learning resources (free): Microsoft Learn paths for C#, ASP.NET Core and Blazor, the official Blazor tutorials and samples, the EF Core docs, and the MudBlazor component docs.

## Access to .NET concept map

Most of what you already do has a direct equivalent. Blazor Interactive Server feels stateful like Access: the component stays alive on the server while the user works, over a live connection. The catch is that state lives per browser tab (a "circuit") and is lost if the connection drops, so the database stays the source of truth.

| In Access | In ASP.NET Core + Blazor | Watch out for |
| --- | --- | --- |
| Table | Entity class + EF Core migration | Schema changes go through migrations, never by hand in prod |
| Relationship window | Navigation properties, `HasOne` / `HasMany`, `OnDelete` | `DeleteBehavior.Restrict` is your "enforce referential integrity" |
| Saved query | LINQ query in a service, or a SQL Server view mapped as keyless entity | N+1 queries: use `Include` or projections with `Select` |
| Bound single form | Blazor component with `EditForm` + model | Validation in the model (DataAnnotations or FluentValidation), not only the UI |
| Continuous form | `MudDataGrid` with server-side paging, sorting, filtering | Always page on the server; never load 50k rows |
| Subform | Child component with a parameter (`AccountId`) | Child reloads when the parameter changes (`OnParametersSetAsync`) |
| Combo box | `MudSelect`, or `MudAutocomplete` for big lists | Large lookups need search-as-you-type |
| AfterUpdate / OnChange | `ValueChanged` / `@onchange` handler in C# | Async all the way; never block with `.Result` |
| Form\_Load / Form\_Current | `OnInitializedAsync` / `OnParametersSetAsync` | Runs twice with prerendering unless you handle it |
| DLookup / DCount | `FirstOrDefaultAsync`, `CountAsync`, `SumAsync` | Push work to the database, not C# loops |
| VBA module | Service class registered with dependency injection | Keep business rules out of components |
| Recordset / open connection | `DbContext` created per operation via `IDbContextFactory` | One long-lived DbContext per circuit causes stale data and threading errors |
| Report | QuestPDF document or SSRS report; ClosedXML export | Page breaks and totals are your code, not a designer |
| Switchboard / nav form | `MainLayout` + `NavMenu` | One layout, every page uses it |
| User-level security / login form | ASP.NET Core Identity, roles, authorization policies | Enforce in services and endpoints, not only by hiding buttons |
| Split FE/BE | App server + SQL Server | Many concurrent users, no file locking |
| Compact & Repair | SQL Server maintenance: backups, index and statistics jobs | Test restores, not just backups |
| AutoExec macro | `Program.cs` startup, hosted services, Hangfire recurring jobs | Scheduled work goes to Hangfire, not a timer in a page |
| Linked Excel import | Import page with preview, processed by a background job | Validate before writing; show row-level errors |

## Data model

Same tables as Rev 1, now as EF Core entities on SQL Server. Every business table inherits a base class with `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`, `IsActive` (soft delete) and `RowVersion` (`rowversion`, for optimistic concurrency), filled automatically by a `SaveChanges` interceptor. Same discipline as your Access audit columns, written once.

| Table | Key columns | Relates to |
| --- | --- | --- |
| Account | Name, VatNumber, IndustryId, Phone, Website, OwnerId, AccountStatusId | Contacts, Opportunities, Activities |
| Address | AccountId, Type (billing/shipping), Street, City, Postcode, Country | Account |
| Contact | FirstName, LastName, Email, Phone, JobTitle, AccountId | Account, Activities |
| Lead | Name, Company, Email, LeadSourceId, Status, ConvertedAt | converts to Account + Contact + Opportunity |
| Opportunity | AccountId, Name, StageId, Amount, Currency, Probability, CloseDate, OwnerId | Account, Activities |
| Stage | Name, SortOrder, IsWon, IsLost, DefaultProbability | Opportunity (lookup table) |
| Activity | ActivityTypeId, Subject, DueAt, DoneAt, OwnerId, RelatedType, RelatedId | any record |
| Note | Body, RelatedType, RelatedId | any record |
| Attachment | StoredPath, OriginalName, SizeBytes, RelatedType, RelatedId | any record |
| Team | Name, ManagerId | Users (each user in one team) |
| Lookup tables | Industry, LeadSource, ActivityType, AccountStatus | real tables, editable by admins |
| AuditLog | UserId, Action, EntityName, EntityId, Changes (JSON in `nvarchar(max)`), ChangedAt | all writes |

Users and roles use the ASP.NET Core Identity tables (`AspNetUsers`, `AspNetRoles` and related), extended with a `TeamId`.

**Polymorphic links:** `RelatedType` + `RelatedId` cannot carry a real foreign key. If you prefer database-enforced integrity (your Access habit), use nullable `AccountId`, `ContactId`, `OpportunityId` columns with a check constraint that exactly one is set. Decide in Phase 1 and log it.

**Decided 8 Oct (spec D1):** typed nullable FKs (AccountId, ContactId, OpportunityId, LeadId) with a check constraint that exactly one is set.

**Additions from the page-by-page spec (8 Oct).** Full column definitions are in the spec's Data dictionary tab.

| Table | Change |
| --- | --- |
| Contact | + OwnerId, Mobile, ImportBatchId |
| Account | + ImportBatchId |
| Lead | + OwnerId, Phone, LeadStatusId, ConvertedAccountId, ConvertedContactId, ConvertedOpportunityId |
| Opportunity | + PrimaryContactId, LostReasonId, ClosedAt |
| Activity | + DurationMinutes, ReminderSentAt; typed FKs instead of RelatedType / RelatedId |
| Note, Attachment | typed FKs instead of RelatedType / RelatedId |
| Lookups | + LeadStatus, LostReason |
| Notification | new: per-user in-app notifications |
| SavedView, Favourite, RecentView | new: per-user list views and starred / recent records |
| ApiToken | new: hashed API tokens with scope and expiry |
| CompanySetting | new: single-row company settings and branding |
| ImportBatch | new: one row per import, for progress and rollback |
| Product, Quote, QuoteLine | Phase 9 only |

Unique rules apply to active rows only, so they are filtered unique indexes (`WHERE IsActive = 1`).

## Requirements

Requirements are written and signed off (by you) in Phase 1, before any UI is built. Each user story becomes a GitHub Issue with its acceptance criteria. Stories and permissions are unchanged from Rev 1; NFRs are adjusted for SQL Server and Blazor.

**Core user stories**

| # | As a... | I want to... | Accepted when |
| --- | --- | --- | --- |
| US1 | Sales user | find a contact by name, email or company, with or without Greek accents | Results appear within 1 second; "Αθηνα" finds "Αθήνα" |
| US2 | Sales user | create an account with contacts in one flow | Contact added from the account page without leaving it |
| US3 | Sales user | convert a lead into account, contact and opportunity | One action creates all three, linked, in one transaction; lead marked converted, not deleted |
| US4 | Sales user | drag an opportunity between pipeline stages | Stage, probability and audit log update; page does not reload; on small screens, stage changes via a dropdown (drag and drop is unreliable on touch) |
| US5 | Sales user | see my tasks due today and overdue | List shows only my open tasks, overdue first |
| US6 | Sales user | get an email reminder for tasks due tomorrow | Email sent once, by a Hangfire recurring job, at 08:00 Athens time |
| US7 | Manager | see my team's pipeline and activities | Visible for my team only, read and reassign |
| US8 | Manager | download a pipeline PDF | One page, totals per stage, generated in under 10 seconds |
| US9 | Admin | import accounts and contacts from Excel | Preview shows row-level errors; nothing is written until confirmed |
| US10 | Admin | manage users, roles and lookup lists | No code change needed to add an industry or lead source |
| US11 | Any user | sign in with Microsoft or a password with 2FA | Both work; failed logins lock out after repeated attempts |
| US12 | Admin | see who changed a record and what changed | Audit log shows user, time, old and new values |
| US13 | Any user | be notified in the app when a record is assigned to me or a task is due | Bell count updates within 2 seconds without a refresh; notifications survive sign-out and app restarts |
| US14 | Sales user | see pipeline and record changes by others as they happen | A card moved by another user moves on my board within 1 second; a form I am editing is never overwritten |
| US15 | Sales user | save a filtered list as a named view | View restores the same filters, sort and columns from any device |
| US16 | Admin | issue and revoke API tokens | Token shown once, stored hashed; revoked token rejected on the next call; token sees only its user's records |
| US17 | Admin | set the company logo and colours | All open sessions show the new branding without a deploy; colours failing WCAG AA are rejected |

**Permission matrix**

| Area | Sales | Manager | Admin |
| --- | --- | --- | --- |
| Own accounts, contacts, opportunities | view, create, edit | view, create, edit | full |
| Team members' records | view only | view, edit, reassign | full |
| Other teams' records | none | view only | full |
| Delete (soft) | own records | team records | all |
| Hard delete / erase a person (GDPR) | none | none | yes |
| Import | none | none | yes |
| Export lists | own records | team records | all |
| Users, roles, lookups | none | none | yes |
| Audit log | own records | team records | all |
| API | same rules as UI, per token | same | same |

Implement record-level rules once, in a query filter or a shared service method, so UI, exports and API cannot drift apart.

**Non-functional requirements**

| Area | Target |
| --- | --- |
| Data volume | Tested with 50,000 contacts, 10,000 accounts, 200,000 activities of seed data |
| Performance | List and search pages under 300 ms server time (p95) at that volume; click to screen update under 100 ms on an office network, under 300 ms on 4G |
| Concurrency | 20 simultaneous users without errors; optimistic concurrency (`rowversion`) on all edits |
| Connection loss | Blazor reconnect UI shown; unsaved edits warned about, not silently lost |
| Browsers | Current Chrome, Edge, Firefox, Safari; core flows (search, contact detail, my tasks, log an activity) usable one-handed at 375 px; tested on a real phone over mobile data, not only Wi-Fi |
| Accessibility | Keyboard navigable forms, labels on all inputs, contrast passing WCAG AA |
| Language and time | English UI, strings in resource files for Greek translation; times stored in UTC, shown in Europe/Athens |
| Availability | Demo is best effort; uptime monitor alerts within 5 minutes of an outage |
| Backups | Lose at most 24 hours of data; restore completed within 30 minutes |
| Security | HTTPS only, HSTS, security headers set, dependencies patched within 7 days of a security alert |

**Design deliverables (Phase 1):** ERD, data dictionary (every field: type, required, default, validation), wireframes in phone and desktop layouts for the 6 key screens (dashboard, account detail, contact list, pipeline board, my tasks, import preview).

**Page-by-page spec:** MexCrm Page-by-Page Spec defines every page, shared component, business rule, the UI and design system, and performance budgets. It adds API tokens, company settings and branding, notifications, live updates, saved views and favourites to the MVP (about 22 hours, inside the slack), and Products and Quotes as a post-MVP Phase 9.

## Phased build plan

Nine MVP phases, about 247 hours of planned work (225 plus 22 added on 8 Oct), then post-MVP Phase 9. Phase 0 is longer than in Rev 1 because C#, async and dependency injection are a bigger jump from VBA than Python. Two sequencing rules hold: a skeleton goes live on staging in Phase 2 so infrastructure problems surface early, and tests are written with each feature (Definition of Done), not saved for the end. The dual-database work from Rev 1 is gone, which frees time for Windows/IIS deployment.

| Phase | Deliverables | Hours | Done when |
| --- | --- | --- | --- |
| 0. Ramp-up | C# fundamentals (types, LINQ, async/await, classes, interfaces), Microsoft Learn ASP.NET Core and Blazor paths, EF Core getting started, Git and GitHub workflow, SQL Server Developer install | 55 | You can build a small Blazor CRUD app over one table from memory and push it to GitHub |
| 1. Requirements & design | User stories as GitHub Issues, permission matrix, NFRs, ERD, data dictionary, wireframes for 6 screens (phone and desktop layouts), polymorphic-link decision | 10 | Backlog and milestones set up in GitHub Projects; design signed off |
| 2. Foundation & walking skeleton | Solution and project layout, `appsettings` per environment + user secrets, base entity with audit columns and `RowVersion`, all entities + migrations, seed data (50k contacts), Identity with roles, CI with tests, Dependabot, "hello world" deployed to MonsterASP staging over HTTPS with WebSockets on (Dockerfile optional) | 25 | Skeleton live on staging; migrations replay on an empty database; CI green |
| 3. Core UI | `MainLayout` + nav, Accounts and Contacts list/detail/edit with `MudDataGrid`, server-side paging and filters, accent-insensitive global search, responsive list and detail pages (card layout on phones) | 25 | US1, US2 pass; lists under 300 ms at seed volume; lists and detail pages usable at 375 px |
| 4. Pipeline & activities | Opportunities with kanban (drag and drop), lead conversion, activities, "my tasks", notes and attachments as child components | 25 | US3, US4, US5 pass |
| 5. Security, data & API | Teams and permission matrix as authorization policies + query filters, audit log interceptor, Entra ID sign-in + 2FA, Excel import as a Hangfire job, ClosedXML export, Web API with token auth and OpenAPI, simple admin pages for lookups | 30 | US7, US9 to US12 pass; matrix enforced in UI and API |
| 6. Reporting & email | Dashboard KPIs, pipeline PDF (QuestPDF or SSRS), Power BI connection (read-only SQL login), transactional email with SPF/DKIM, task reminder recurring job | 15 | US6, US8 pass; Power BI report refreshes from the demo database |
| 7. Production & operations | Production deploy on MonsterASP (crm.mexdb.com once mapped), always-on and keep-alive verified, demo mode with nightly reset, backups to B2 + tested restore, uptime monitor, Sentry, runbooks; optional Linux container run | 20 | Restore done in under 30 minutes; runbooks followed once each; app runs on IIS |
| 8. Hardening & docs | Playwright tests for the 5 core flows, bUnit tests for key components, performance pass at seed volume, accessibility pass, Playwright core flows also run in a phone viewport, PWA manifest and icon (add to home screen), README and user guide, retrospective | 20 | All objectives in the success criteria table met |
| 9. Products and quotes (post-MVP) | Products CRUD, quote editor with lines and totals, revisions, quote PDF, accept sets the opportunity to Won, expiry job; QuickBooks / Xero hand-off later | 25 to 30 | Products module built in under 4 hours (objective 1); a quote goes from draft to accepted and PDF |
| Total |  | 247 | 225 planned plus about 22 for the 8 Oct additions (Phases 3 to 7); about 53 hours of slack remain and the mobile work fits within it |

**No free admin screen:** unlike Django Admin, ASP.NET Core has no built-in back office. Build one generic lookup-editing page in Phase 5 and reuse it for every lookup table; use SSMS for data fixes until then.

### Indicative pace

A pace guide, not a deadline. At about 15 hours a week, each phase takes roughly the weeks shown; rounding up to whole weeks leaves about 75 hours of slack for extra learning. Set each phase up as a milestone in GitHub Projects, without due dates.

| Phase | About | Milestone |
| --- | --- | --- |
| 0. Ramp-up | 4 weeks |  |
| 1. Requirements & design | 1 week |  |
| 2. Foundation & walking skeleton | 2 weeks | Staging live on MonsterASP |
| 3. Core UI | 2 weeks |  |
| 4. Pipeline & activities | 2 weeks |  |
| 5. Security, data & API | 3 weeks |  |
| 6. Reporting & email | 2 weeks |  |
| 7. Production & operations | 2 weeks | Demo live on MonsterASP (crm.mexdb.com once mapped) |
| 8. Hardening & docs | 2 weeks | MVP complete |
| Total | about 20 weeks |  |

If a phase runs long, let it. Understanding beats finishing on time.

## Architecture and coding conventions

One solution, three projects plus tests. Business logic lives in services, so components stay thin and logic stays testable without a browser.

```text
Crm.sln
  src/
    Crm.Web/           Blazor Web App: Program.cs, Components/ (Pages, Layout, Shared), API endpoints, Hangfire setup
    Crm.Core/          entities, services (LeadService.ConvertAsync), validation, interfaces, permission rules
    Crm.Data/          CrmDbContext, entity configurations, migrations, audit interceptor, seed data
  tests/
    Crm.Core.Tests/    xUnit unit tests for services and permission rules
    Crm.Web.Tests/     bUnit component tests
    Crm.E2E/           Playwright end-to-end tests
  docs/                adr/, runbooks/, data-dictionary.md
```

Conventions worth setting on day one:

- **Follow .NET naming.** PascalCase entities and properties, no `tbl` prefix (EF Core maps `Account` to `Accounts`). Keep your surrogate integer PKs (`int Id`, identity) and real lookup tables.
- **Constraints in the database, not only in C#.** Unique indexes, check constraints and `DeleteBehavior.Restrict` in entity configurations, so imports and scripts cannot sneak in bad data.
- **Fat services, thin components.** `LeadService.ConvertAsync(leadId, user)` is the equivalent of a public VBA sub; the component just calls it and shows the result.
- **`IDbContextFactory` in Blazor.** Create a short-lived `DbContext` per operation (`await using var db = ...`). Never inject one DbContext into a component for its whole life.
- **Async all the way.** `await` every database call; `.Result` and `.Wait()` deadlock or freeze the circuit.
- **Secrets outside Git.** User secrets in development, environment variables in production; an `appsettings.Example.json` lists what is needed. Production values never go in appsettings.json, Git or a chat window; if one leaks, rotate it at once (BookPicker lesson).
- **Every change through Git + migrations.** Feature branch, pull request (to yourself), CI runs tests, merge, deploy. Production migrations run as a generated, idempotent SQL script you review first.
- **UTC everywhere.** Store `DateTime` in UTC (or `DateTimeOffset`), convert to Europe/Athens only when displaying.
- **Mobile-friendly from day one.** Use MudBlazor breakpoints; list pages switch from `MudDataGrid` to a card or list layout below the `md` breakpoint. Use the right input types (`tel`, `email`, `date`) so phones show the right keyboard, and keep touch targets at least 44 px. Retrofitting this later costs far more.
- **Localization from the start.** UI strings in `.resx` resource files via `IStringLocalizer`; adding Greek later costs hours instead of days.
- **Nullable reference types on, warnings as errors.** The compiler catches the null bugs VBA lets through at runtime.
- **Use Claude/AI as a pair programmer, not an author.** You review and understand every line; understanding the code is the point of this project.

**Workflow**

- **Trunk-based branching:** short-lived feature branches off `main`, merged by pull request when CI is green. `main` is always deployable.
- **Releases:** tag each phase gate (`v0.2.0` for Phase 2, and so on); keep a `CHANGELOG.md`.
- **Architecture decision records:** one short markdown file per significant choice in `docs/adr/`, mirrored in the decision log below.
- **Review:** as a solo developer, read your own diff in the pull request the next day before merging, or ask an AI reviewer for a second pass.

### Definition of Done and phase gates

A feature is done only when every box below is true. Copy this checklist into the GitHub pull request template so it appears on every change.

- [ ] Linked to a GitHub Issue whose acceptance criteria pass
- [ ] Unit tests for new service methods and permission rules; bUnit test for new interactive components; Playwright test if it is a core flow
- [ ] Migration committed and replayable on a fresh database
- [ ] Build has no warnings, `dotnet format` clean, CI green
- [ ] No new N+1 queries (checked with EF Core logging of SQL in development)
- [ ] Deployed to staging.mexdb.com and clicked through
- [ ] Checked at 375 px in browser dev tools (device emulation)
- [ ] README, data dictionary or runbook updated if behaviour changed

**Phase-gate review (15 minutes, end of each phase):** what was harder than expected, hours spent vs planned (calibration for estimating web work), re-estimate the remaining phases, and log any decisions in the decision log.

## Deployment paths: cloud and on-prem

Staging and demo run on MonsterASP: Windows/IIS hosting with .NET 10 and SQL Server in the same datacenter, published from Visual Studio with Web Deploy, the same route as BookPicker. Blazor Interactive Server needs WebSockets and an app that stays alive, so verify both, plus the database size cap and custom domain mapping, before relying on the free tier; upgrade the tier if any check fails. The Linux container path from the original Rev 2 is now optional practice.

| Environment | Setup | Address |
| --- | --- | --- |
| Local dev | Visual Studio + SQL Server 2025 Developer, full 50k seed data | localhost |
| Staging | MonsterASP site + its SQL Server database; smaller seed if the size cap requires | runasp.net address (staging.mexdb.com once mapped) |
| Demo | MonsterASP site + database, demo mode on, nightly reset | crm.mexdb.com once mapped |
| Linux container (optional) | Docker on any Linux host, e.g. Oracle Always Free VM with Azure SQL (x86 SQL Server image cannot run on its ARM VMs) | not public |
| Azure (optional) | App Service for Linux (Basic tier or above for WebSockets and Always On) + Azure SQL, only on free credits with a $0 budget alert set first | azure.mexdb.com |

**Domain setup for mexdb.com**

- Before changing anything, export the current DNS records at Domain.com: MX (email), TXT (SPF, DKIM) and any A/CNAME for an existing site. Use subdomains only so nothing running breaks.
- Check whether the MonsterASP plan allows custom domains; if so, add the crm and staging records it asks for (CNAME or A) at Domain.com.
- Certificates: MonsterASP's free Let's Encrypt, enabled per site in the panel (as for nextread.runasp.net).
- Optional: move DNS to Cloudflare free. Copy every existing record first, then switch nameservers. Its proxy supports WebSockets; set SSL mode to Full (strict).

Deployment checklist, every environment:

- `ASPNETCORE_ENVIRONMENT=Production`, detailed errors off, HTTPS redirection and HSTS on
- Nginx (or IIS) proxies WebSockets: `Upgrade` and `Connection` headers forwarded, idle timeout raised; forwarded headers middleware configured so the app sees the real scheme and client IP
- Data Protection keys persisted to a volume or the database, or every restart logs users out
- Migrations applied from a reviewed idempotent script, never automatically on startup in production
- Database backed up and restore tested before go-live
- Logs (Serilog) and error alerts (Sentry) working
- Deploy to staging first, then publish the same build to the demo site

## Operations

Once crm.mexdb.com is public, it needs the same care as a production app: patched, backed up, watched, and documented well enough to rebuild from scratch.

**Email**

- Transactional SMTP account on a free tier, sent through MailKit; send from `crm@mexdb.com`.
- Add the provider's SPF and DKIM records plus a DMARC record (`p=none` to start). Merge with any existing SPF record; a domain can have only one.
- Test password reset and task reminders on staging first, using a separate sender address.

**Backups**

- Check what backups the MonsterASP plan includes and how a restore works; never rely on the host's backups alone.
- Nightly or weekly database backup (panel backup or a `.bacpac` export with SqlPackage) plus the attachments folder, pulled to your PC by a scheduled job and pushed to Backblaze B2 with `rclone`, so a copy lives outside the host.
- Keep 7 daily and 4 weekly copies; encrypt with `rclone crypt` so the bucket holds no readable personal data.
- On SQL Server Express (typical small client on-prem), there is no SQL Agent: schedule `BACKUP DATABASE` with Windows Task Scheduler + `sqlcmd`.
- Monthly restore drill to a scratch database; record the time taken.

**Monitoring**

- Sentry for application errors (alerts to email); Serilog to file for the rest.
- UptimeRobot checking `https://crm.mexdb.com/health` every 5 minutes; the ASP.NET Core health check includes the database.
- Watch disk and database usage against the MonsterASP plan caps (check monthly, alert at 80% if the panel offers it); attachments are the usual cause of growth.

**Hosting hygiene**

- MonsterASP account with a strong unique password and 2FA if offered; publish profiles and connection strings never in Git.
- The app's SQL login has only the rights it needs; Power BI uses a separate read-only login.
- Remote database access limited to your own IP where the panel allows it.
- Optional Linux VM path only: SSH keys, `fail2ban`, `unattended-upgrades`, only ports 22, 80 and 443 open, containers as a non-root user.

**Runbooks (in `docs/runbooks/`, each followed once to prove it works)**

1. Deploy a new release and roll back to the previous image tag
2. Restore the database and attachments from backup
3. Rebuild the site from zero on a new host (create site and database, publish, DNS, certificates, restore)
4. Renew or reissue certificates manually
5. Rotate secrets (SQL password, API tokens, Data Protection keys)

## Risks, edge cases and gotchas

The items most likely to bite an experienced Access developer moving to ASP.NET Core and Blazor:

| Risk | Why it matters | Mitigation |
| --- | --- | --- |
| DbContext lifetime in Blazor | A scoped DbContext lives as long as the circuit: stale data, memory growth, "second operation started" threading errors | `IDbContextFactory`, one short-lived context per operation |
| Connection drops | Interactive Server state lives on the server; a lost WebSocket loses unsaved form input | Reconnect UI, save often, keep forms short; database is the source of truth |
| Hosting needs WebSockets and always-on | Free app tiers sleep or limit connections; misconfigured proxies break the app silently | MonsterASP with WebSockets on and always-on verified; UptimeRobot keep-alive; upgrade the tier if the free one sleeps; sticky sessions if you ever run more than one instance |
| Concurrent edits | No record locking on the web; last save silently wins | `rowversion` column; catch `DbUpdateConcurrencyException` and show "changed by someone else" |
| Greek text search | Default collations treat "Αθήνα" and "Αθηνα" as different | Accent-insensitive Greek collation such as `Greek_100_CI_AI` on searchable columns; SQL Server full-text search for larger text |
| SQL Server on ARM | The SQL Server container image is x86-only and Azure SQL Edge is retired, so it cannot run on Oracle's free Ampere VMs | Only affects the optional Linux path: the database stays on MonsterASP or Azure SQL; SQL Server locally and in CI (GitHub runners are x86) |
| Free database limits | Free database tiers cap size and compute (MonsterASP, Azure SQL) | Check the caps before go-live; staging uses a smaller seed if needed and the full 50k seed stays local; never choose a billing overage option |
| Library licences | Several .NET libraries went commercial (EPPlus, FluentAssertions, MediatR, AutoMapper) | Check the licence before adding a package; prefer MIT/Apache |
| Personal data (GDPR) | A CRM holds personal data by definition, and the demo is public | Fake seed data only; still build export and erase-a-person actions as practice |
| Security | Unlike Access on a LAN, the demo and its API are reachable from the internet | HTTPS only, authorization on every page and endpoint (`[Authorize]` by default), lockout on failed logins, rate limiting on the API, keep .NET patched |
| N+1 queries | List pages and API endpoints fire one query per row and slow down at scale | Projections with `Select`, `Include` where needed, EF Core SQL logging in development |
| Large imports | Excel imports with 20k rows time out or freeze the circuit | Run as a Hangfire job, show progress, write in batches inside a transaction |
| File attachments | Disk fills, backups miss them | Store outside wwwroot and the publish folder (a publish can wipe it), include in backups, cap file size and allowed types |
| Learning curve | C#, async, DI and web concepts at once, around a full-time job | Phase 0 is non-negotiable; build the small CRUD app before touching the CRM |
| Free-tier cold starts | A sleeping site takes 10 to 30 s to load (seen on nextread.runasp.net) and stops Hangfire jobs | UptimeRobot 5-minute keep-alive; verify always-on; upgrade the tier before showing the demo to clients if it still sleeps |
| Secrets leaking | Happened on BookPicker: production values overwrote appsettings.json and were pasted into a chat | User secrets locally, production file outside Git, never paste config files; rotate any exposed key or password at once |
| Live updates on one server | Notifications and live board use an in-memory event broker that only reaches circuits on the same server instance | Fine for one instance; notifications are stored in the database so nothing is lost on restart; a scale-out needs a backplane (e.g. Redis) |

## Decision log

Every significant choice gets a row here and an ADR file in the repo. Newest first.

| Date | Decision | Reason |
| --- | --- | --- |
| 2026-10-04 | Rev 2: switch from Django + PostgreSQL + HTMX to ASP.NET Core + Blazor + SQL Server | Better fit for the Microsoft-shop Access/VBA replacement market; reuses SQL Server, SSRS and Power BI skills |
| 2026-10-04 | Blazor Interactive Server, not WebAssembly | Internal business app; closest to Access forms; direct database access from server code |
| 2026-10-04 | Drop the dual-database goal; SQL Server only | Removes Rev 1's biggest technical risk; target clients run SQL Server |
| 2026-10-04 | Demo app on Oracle Always Free VM, database on Azure SQL free offer (superseded 2026-10-08 by MonsterASP) | SQL Server cannot run on ARM; keeps the demo at zero cost |
| 2026-10-04 | Hangfire for background jobs | Free core, SQL Server storage, built-in dashboard, widely used |
| 2026-10-04 | No fixed dates; pace guide assumes about 15 h/week | Placeholder until confirmed; dates shift if either changes |
| 2026-10-07 | BookPicker as the Phase 0 practice app | A small real app end to end (Blazor, EF Core, external APIs, deployment) before the CRM; doubles as a portfolio piece |
| 2026-10-07 | MonsterASP free tier for BookPicker (nextread.runasp.net) | Worked first time with Web Deploy from Visual Studio; .NET 10 and SQL Server included |
| 2026-10-08 | MonsterASP replaces Oracle VM + Azure SQL as the main hosting path; Docker/Linux optional | Database in the same datacenter as the app, simpler operations, matches clients' Windows hosting; supersedes the 2026-10-04 hosting decision |
| 2026-10-08 | MVP additions: API tokens, settings and branding, notifications, live updates, saved views, favourites, demo mode | The API needs tokens; branding makes each client deployment theirs; live features show what Blazor Server does; demo mode serves the portfolio |
| 2026-10-08 | Products and Quotes post-MVP (Phase 9); invoices and orders out of scope | Keeps the MVP on track; invoicing is regulated almost everywhere and clients already run accounting software |
| 2026-10-08 | Typed nullable FKs for activities, notes and attachments | Real referential integrity and simpler queries than RelatedType / RelatedId |
| 2026-10-08 | QuestPDF for the pipeline PDF and quotes | Deploys with the app; no report server to host on shared hosting |
| 2026-10-08 | List and search target 300 ms server time (was 500 ms) | Every Blazor Server click is a round trip, so server time is the main speed lever |

## Post-MVP roadmap

In rough priority order, each a self-contained learning step after the demo is live:

1. Products and Quotes (Phase 9, specified in the page-by-page spec)
2. QuickBooks Online / Xero hand-off: accepted quote becomes a draft invoice via their API
3. Power Apps version of one module (e.g. activities) on the same SQL Server data, to compare low-code and custom
4. Activity calendar view, week and month (Heron.MudCalendar)
5. Merge duplicate accounts and contacts
6. Web-to-lead public form
7. Parent and child account hierarchy
8. GDPR data export for a person
9. Greek translation of the UI (`.resx` localization)
10. Custom fields per entity (JSON column with SQL Server JSON functions)
11. Email integration: log emails against contacts via Microsoft Graph
12. Access-to-SQL Server migration script for an existing Access CRM back end (SSMA plus cleanup scripts)
13. Multi-tenancy (tenant column with EF Core global query filters, or database per tenant)
14. Docker / Linux deployment practice (the original Rev 2 hosting path)

## Open questions

- [x] Links for activities, notes and attachments: typed nullable FKs (decided 8 Oct, spec D1)
- [x] Pipeline PDF: QuestPDF (decided 8 Oct)
- [x] Start: Phase 0 started 7 Oct; the pace guide stays a rough reference at about 15 h/week
- [ ] MonsterASP: verify always-on, WebSockets, database size cap and custom domain mapping before Phase 2 staging

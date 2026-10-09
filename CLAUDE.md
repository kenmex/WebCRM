# WebCRM

Learning / proof-of-capability CRM built as a web app, modeled on the CRMs I build in MS Access. Developer: Kostas (experienced in Access/VBA, SQL Server, C#; new to the web toolchain).

## Source documents (read before any design decision)
- `docs/rev2-plan.md`: project plan, scope, architecture, conventions, phases, risks.
- `docs/page-spec.md`: every page, shared component, business rule and performance budget.
- `docs/data-dictionary.md`: every table, column, type, FK, index and constraint. It is the source of truth for the schema: change it there first, then in code. Implement it exactly; ask before deviating.

## Stack (fixed, do not deviate)
- C# / ASP.NET Core on .NET 10
- Blazor Web App, Interactive Server render mode set globally with `prerender: false`; Identity/Account pages excluded from interactive routing (static server rendering)
- SQL Server 2025 Developer on localhost (SSMS for inspection)
- EF Core (code-first migrations), `IDbContextFactory` with one short-lived context per operation
- ASP.NET Core Identity for users and roles (Admin, Manager, Sales)
- MudBlazor for all UI components; no second UI library
- Visual Studio 2026 Community as the IDE
- No JavaScript frameworks or Node.js tooling. Only write JS interop if there is no Blazor/MudBlazor way to do it, and say so first.

## Solution layout
Per the Architecture section of `docs/rev2-plan.md`, using `WebCRM.*` names:
- `src/WebCRM.Web`: Blazor app, Program.cs, components, API endpoints
- `src/WebCRM.Core`: entities, services (business rules), validation, interfaces
- `src/WebCRM.Data`: DbContext, entity configurations, migrations, interceptors, seed data
- `tests/WebCRM.Core.Tests` (xUnit + Shouldly), later `tests/WebCRM.Web.Tests` (bUnit) and `tests/WebCRM.E2E` (Playwright)

## Hosting
- Target: MonsterASP (staging and demo). Keep the app deployable there: no features that require a paid tier or a dedicated server.
- Docker/Linux is optional, not a requirement.

## Scope
- MVP and post-MVP as defined in `docs/rev2-plan.md` and `docs/page-spec.md`.
- Out of scope: invoices and orders (handed off to the client's accounting system).
- Must be mobile-friendly: every page has to work at 375 px width.

## Conventions
- Schema exactly as `docs/data-dictionary.md`: singular entity classes, no tbl prefix, `int Id` identity PKs, base columns (CreatedAt, CreatedBy, UpdatedAt, UpdatedBy, IsActive, RowVersion) filled by a SaveChanges interceptor.
- Lookup values are real tables, not enums or hard-coded lists.
- Constraints in the database (unique/filtered indexes, check constraints, `DeleteBehavior.Restrict`), not only in C#.
- Business rules live in services in WebCRM.Core, never in components.
- Connection strings and secrets go in `dotnet user-secrets`, never in appsettings.json or git.
- Async all the way for data access. No raw SQL unless there is a clear reason.
- Times stored in UTC, shown in Europe/Athens.
- Check the licence before adding any NuGet package; prefer MIT/Apache (avoid EPPlus, FluentAssertions v8+, MediatR, AutoMapper).

## How to work with me
- Explain web-specific concepts briefly when they first come up (Blazor lifecycle, DI, middleware). Skip basics of C# and SQL.
- Before large changes (new project, new migration, restructuring), say what you will do and wait for my OK.
- Run `dotnet build` after changes and fix errors before reporting done.
- I may have Visual Studio open and debugging; if a build fails on locked files, tell me rather than retrying.
- Small commits with clear messages.
- Never use em dashes in any text you write.

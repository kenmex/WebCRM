# WebCRM

Learning / proof-of-capability CRM built as a web app, modeled on the CRMs I build in MS Access. Developer: Kostas (experienced in Access/VBA, SQL Server, C#; new to the web toolchain).

## Stack (fixed, do not deviate)
- C# / ASP.NET Core on .NET 10
- Blazor Web App, Interactive Server render mode
- SQL Server 2025 Developer on localhost (SSMS for inspection)
- EF Core (code-first migrations)
- MudBlazor for all UI components
- Visual Studio 2026 Community as the IDE
- No JavaScript frameworks or Node.js tooling. Only write JS interop if there is no Blazor/MudBlazor way to do it, and say so first.

## Hosting
- Target: MonsterASP (staging and demo). Keep the app deployable there: no features that require a paid tier or a dedicated server.
- Docker/Linux is optional, not a requirement.

## Scope
- MVP: core CRM (companies, contacts, activities/notes, users and roles).
- Post-MVP: Products and Quotes.
- Out of scope: invoices and orders (handled by the client's accounting system).
- Must be mobile-friendly: every page has to work at phone width.

## Conventions
- Database: singular entity classes, plural table names with no tbl prefix (EF Core convention), surrogate int identity PKs, and audit columns on every table (CreatedAt, CreatedBy, ModifiedAt, ModifiedBy).
- Lookup values are real tables, not enums or hard-coded lists.
- Connection strings and secrets go in `dotnet user-secrets`, never in appsettings.json or git.
- Async all the way for data access. No raw SQL unless there is a clear reason.

## How to work with me
- Explain web-specific concepts briefly when they first come up (Blazor lifecycle, DI, middleware). Skip basics of C# and SQL.
- Before large changes (new project, new migration, restructuring), say what you will do and wait for my OK.
- Run `dotnet build` after changes and fix errors before reporting done.
- I may have Visual Studio open and debugging; if a build fails on locked files, tell me rather than retrying.
- Small commits with clear messages.
- Never use em dashes in any text you write.

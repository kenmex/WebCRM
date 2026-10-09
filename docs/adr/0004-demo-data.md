# ADR 0004: Demo data tool, and the "Demo" ImportBatch marker

Date: 2026-10-09
Status: Accepted

## Context

The lists, the global search and the budgets in `docs/page-spec.md` (300 ms for list pages, 1 s for search) mean nothing on an empty database. We need realistic volume on a developer machine, repeatably, without touching the developer's own records, and without any of it ending up on a hosted server. Later a public demo needs the same kind of data, reset every night.

## Decision

A separate console project, `src/WebCRM.DemoData`, run by hand:

```
dotnet run --project src/WebCRM.DemoData -- seed        # wipe the previous demo data, make a new set
dotnet run --project src/WebCRM.DemoData -- wipe        # remove the demo data only
dotnet run --project src/WebCRM.DemoData -- measure     # time the lists and search against the budgets
```

- **Never deployed.** `WebCRM.Web` does not reference it, so Bogus and the tool are not in what goes to MonsterASP. Bogus (MIT) is used for English, German and Greek person and company names; Greek cities, streets, phones and emails are hand-written pools with correct accents, because Bogus's are not usable.
- **Refuses to run** unless the environment is `Development` and the SQL Server is on this machine (`localhost`, `.`, `(local)`, `127.0.0.1`, this machine's name). The launch profile sets `DOTNET_ENVIRONMENT=Development`.
- **Repeatable.** A fixed seed and a "today" date give identical data; both are options. Sizes are options too (defaults: 10,000 accounts, 50,000 contacts, 4,000 leads, 15,000 opportunities, 200,000 activities).
- **Fast and all-or-nothing.** `SqlBulkCopy` with the ids chosen in memory (no read-back), constraint checking on (so the constraints stay trusted and a bad row fails the run), all in one transaction together with the wipe. Every row is stamped with the system user (ADR 0002).
- **Demo users** (one Admin, two Managers in two teams, eight Sales) have ids starting with `demo-`, emails `...@demo.webcrm.local`, and all share the password in the user-secret `Demo:Password`.
- It adds the lead sources and lost reasons that are missing (by name; existing ones are left alone), because realistic leads and lost deals need them.

## How demo data is recognised (no schema change)

The wipe deletes demo data and nothing else, so it needs a reliable definition of "demo":

- **Accounts and contacts** carry `ImportBatchId` of one fixed `ImportBatch` row with `Entity = 'Demo'` and `FileName = 'demo-data'`.
- **Opportunities, leads and activities** are demo data when the system user created them **and** they belong to demo records (or, for leads, a demo owner). Records created by a person never match, because they are stamped with that person.
- **Users** are the ones whose id starts with `demo-`; **teams** are the ones named `Demo: ...`.
- If anything of the developer's own is tied to demo data (a contact added to a demo account, a record reassigned to a demo user, a note on a demo record), the wipe **stops with a list and changes nothing**.

## The "Demo" ImportBatch is a marker, not an import

It lives in `ImportBatches` only because accounts and contacts already point there. The import feature (P23) must treat it accordingly:

- The import history list must **not show** a batch with `Entity = 'Demo'`.
- Import **rollback must never roll it back**, and must refuse if asked. Only the demo tool removes it.
- Counting or reporting on imports (dashboard, audit, "records imported") must exclude it.

Any query over `ImportBatches` that powers a user-facing page needs `Entity <> 'Demo'`.

## The public demo (Phase 7) does not run Bogus on the server

The public demo needs to reset to the same data every night. It will **restore a snapshot made by this tool** on a developer machine (a database backup, or an export of the demo tables), not generate data on the server. So Bogus and this project stay out of the deployed app, the reset is fast and cannot fail halfway on a random-data edge case, and what visitors see is exactly what was checked locally. The nightly job restores the snapshot; the tool is only ever run by a developer to make a new one.

## Consequences

- The tool must be kept in step with the schema when columns or rules change. Its tests generate data and check it against the real database constraints on a throwaway database, so a change that makes demo data invalid fails the build.
- Demo accounts and contacts must never be treated as imported rows in P23.
- The password is a user-secret, never in code or in git. Changing it means running `seed` again (about a minute).

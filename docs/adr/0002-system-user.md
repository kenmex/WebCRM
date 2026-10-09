# ADR 0002: Built-in system user for non-interactive writes

Date: 2026-10-09
Status: Accepted

## Context

`CreatedBy` and `UpdatedBy` are foreign keys to `AspNetUsers`, and the audit interceptor refuses to save audited rows without a user. Work that happens outside a signed-in session (the start-up seeder now, imports and background jobs later) still has to write audited rows.

## Decision

Seed one built-in account and use it as the identity for such work:

- Fixed Id `00000000-0000-0000-0000-000000000001` (`SystemUser.Id`), UserName `system`, DisplayName `System`.
- No email, no password, `IsActive = false`, locked out permanently (`LockoutEnd = DateTimeOffset.MaxValue`). It can never sign in.
- All notification flags are off.
- The seeder creates it first and stamps its own writes with it through a dedicated `ICurrentUser`.

## Reasons

- Keeps the FKs, the interceptor and the data dictionary unchanged: no nullable audit columns and no special cases in the schema.
- A real row means audit history always resolves to a name.

## Consequences

The system user is a real row in `AspNetUsers`, so everything that lists users must exclude it (filter on `Id <> SystemUser.Id`, or on `IsActive` where that already applies):

- Owner pickers (leads, accounts, contacts, opportunities, activities).
- The Users page (P20).
- The last-active-Admin rule. It has no Admin role, so it never counts, but any user count or "other active Admins" query must not include it.
- Any user-based reporting or assignment rule.

Audit displays (CreatedBy/UpdatedBy) should show it as "System".

# ADR 0001: Keep passkey sign-in from the .NET 10 template

Date: 2026-10-09
Status: Accepted

## Context

Identity was added from the official .NET 10 Blazor Web App template (individual accounts). That template includes passkey (WebAuthn) sign-in: a "Log in with a passkey" option on the sign-in page, a Passkeys section under My account, and the `AspNetUserPasskeys` table (Identity schema version 3), created by the InitialIdentity migration.

The page spec (P1 to P5) and the data dictionary do not list passkeys. Sign-in there is a password with optional 2FA, plus Microsoft sign-in in Phase 5.

## Decision

Keep passkey sign-in as the template provides it.

## Reasons

- Free: part of ASP.NET Core Identity, no extra package or service.
- Modern: phishing-resistant sign-in with Windows Hello, Touch ID or a phone, increasingly expected by business users.
- No custom JavaScript: the only script is the template's own `PasskeySubmit.razor.js`, maintained by Microsoft. CLAUDE.md's no-custom-JS rule is not affected.
- Removing it means editing many template files and the schema version, and diverging from the official pattern for no gain.

## Consequences

- `AspNetUserPasskeys` is part of the schema. It is an Identity table, so it stays outside the data dictionary along with the other Identity tables beyond User.
- The passkey UI gets restyled together with the other Identity pages (P1, P5).
- Passkeys are bound to the site's domain: credentials registered on localhost or the runasp.net address will not work on crm.mexdb.com. Users register them again after a domain change.
- Add passkey sign-in to the P1 and P5 entries in the page spec when those pages are built.

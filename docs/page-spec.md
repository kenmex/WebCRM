# MexCrm Page-by-Page Spec

Oct 8, 2026 · @Kostas

## Purpose

MexCrm has 26 pages in the MVP, built around 4 record types (Accounts, Contacts, Leads, Opportunities) that share one list, detail and edit pattern. This spec defines each page: route, who sees it, what it shows, what it does, and the rules behind it. It is the Phase 1 design deliverable from the Rev 2 plan, and the source for GitHub Issues.

How to use it:

- Build the shell and the common patterns once (sections 2, 4, 6), then each record page is mostly configuration.
- Each page spec lists its user stories (US1 to US12 from the Rev 2 plan), so acceptance criteria stay traceable.
- Open decisions are collected at the end. Settle them before the phase that needs them, not now.
- Scope is the MVP list in the Rev 2 plan. Anything here that goes beyond it is marked **(post-MVP)**.

## App shell

Every signed-in page uses one `MainLayout`: a top bar, a left nav drawer, and the page body. It is the Access switchboard, built once.

**Top bar (all screen sizes)**

- App name (links to Dashboard), drawer toggle on small screens.
- Global search box: type 2+ characters, dropdown shows the top 5 hits per type (Accounts, Contacts, Opportunities, Leads); Enter opens the full Search page. Accent-insensitive (US1).
- **+ New** button: menu with Account, Contact, Lead, Opportunity, Task. Opens the create form for that type.
- User menu: name and role, My account, Sign out.

Also in the top bar:

- **Notifications bell** with unread count: assigned to you, task due today, your opportunity changed by someone else. Pushed live over the Blazor circuit, no polling. Stored in the database, so live push is best effort: after a reconnect or app restart the bell reloads from the database. Click marks read and opens the record.
- **Recently viewed and favourites**: the search dropdown shows the last 10 records and starred records before any typing.
- **Command palette (Ctrl+K)**: jump to any record, page or action (New account, Log activity). Reuses global search.
- **Theme toggle**: light, dark, or follow the system.

**Nav drawer, by role**

| Item | Sales | Manager | Admin |
| --- | --- | --- | --- |
| Dashboard | yes | yes | yes |
| My tasks (badge = overdue count) | yes | yes | yes |
| Accounts, Contacts, Leads | yes | yes | yes |
| Opportunities, Pipeline | yes | yes | yes |
| Activities | yes | yes | yes |
| Reports | no | yes | yes |
| Admin: Users, Teams, Lookups, Import, Audit log, API tokens, Settings | no | no | yes |

Hiding a nav item is cosmetic. Every page also carries `[Authorize(Policy = ...)]`, and every service call checks the record-level rule.

**Mobile (below the `md` breakpoint, about 960 px)**

- Drawer collapses to a hamburger; the top bar keeps search as an icon that expands full width.
- A bottom bar with 4 targets: Dashboard, My tasks, Search, + New. These are the core phone flows from the NFRs.
- Grids switch to card lists; detail tabs become stacked collapsible panels.

**Always present**

- Blazor reconnect overlay (styled, not the default) with a "you have unsaved changes" notice when an edit form is dirty.
- Snackbar area for save confirmations and errors (`MudSnackbar`).
- Breadcrumb on detail pages: List > Record name.

## Site map

26 pages: 5 sign-in pages (static server rendering, Identity), 14 app pages and 7 admin pages, all Interactive Server. Create and edit have no routes of their own: `/accounts/new` and the Edit button reuse the detail page's form (see section 4).

| # | Page | Route | Roles | Phase | Stories |
| --- | --- | --- | --- | --- | --- |
| P1 | Sign in | `/Account/Login` | public | 2 | US11 |
| P2 | Two-factor code | `/Account/LoginWith2fa` | public | 5 | US11 |
| P3 | Forgot password | `/Account/ForgotPassword` | public | 6 | US11 |
| P4 | Reset password | `/Account/ResetPassword` | public | 6 | US11 |
| P5 | My account | `/Account/Manage` | all | 5 | US11 |
| P6 | Dashboard | `/` | all | 6 | US5, US7 |
| P7 | Search results | `/search?q=` | all | 3 | US1 |
| P8 | Accounts list | `/accounts` | all | 3 | US1 |
| P9 | Account detail / new | `/accounts/{id}`, `/accounts/new` | all | 3 | US2, US12 |
| P10 | Contacts list | `/contacts` | all | 3 | US1 |
| P11 | Contact detail / new | `/contacts/{id}`, `/contacts/new` | all | 3 | US2 |
| P12 | Leads list | `/leads` | all | 4 |  |
| P13 | Lead detail / new + Convert | `/leads/{id}`, `/leads/new` | all | 4 | US3 |
| P14 | Opportunities list | `/opportunities` | all | 4 |  |
| P15 | Pipeline board | `/pipeline` | all | 4 | US4, US7 |
| P16 | Opportunity detail / new | `/opportunities/{id}`, `/opportunities/new` | all | 4 | US4 |
| P17 | My tasks | `/tasks` | all | 4 | US5 |
| P18 | Activities | `/activities` | all | 4 | US7 |
| P19 | Reports | `/reports` | Manager, Admin | 6 | US8 |
| P20 | Users | `/admin/users` | Admin | 5 | US10 |
| P21 | Teams | `/admin/teams` | Admin | 5 | US7, US10 |
| P22 | Lookups (incl. stages) | `/admin/lookups/{type}` | Admin | 5 | US10 |
| P23 | Import | `/admin/import` | Admin | 5 | US9 |
| P24 | Audit log | `/admin/audit` | Admin | 5 | US12 |
| P28 | API tokens | /admin/api-tokens | Admin | 5 |  |
| P29 | Company settings and branding | /admin/settings | Admin | 5 |  |

System pages, built in Phase 2 with the shell: Not found (404), Access denied (403), Error (with a correlation id, never a stack trace), and About (app version, build date, environment) linked from the user menu.

## Common page patterns

Three patterns cover almost every page: **List**, **Detail** and **Child panel**. Build them as generic components in Phase 3; each record type then supplies columns, fields and a service.

### List page (the continuous form)

- `MudDataGrid` with `ServerData`: paging (25 per page), sorting and filtering run in SQL, never in memory.
- Scope chips above the grid: **Mine**, **My team** (Manager), **All I can see**. Default is Mine for Sales, My team for Manager.
- Quick filter text box (accent-insensitive) plus 2 to 4 typed filters per entity (status, owner, date range).
- Filters, sort and page live in the URL query string (`?scope=team&status=2&page=3`), so Back, refresh and shared links restore the view.
- Row click opens the detail page. Checkbox column for bulk actions: Reassign owner (Manager, Admin), Export selected.
- **Export** button: current filter to .xlsx via ClosedXML, same record scope as the grid, capped at 50,000 rows.
- Phone: card list (name, 2 key fields, owner), infinite scroll or Load more instead of page numbers.
- Empty state with a + New button; "no results" state with Clear filters.

* **Saved views**: Save the current filters as a named view ("My hot deals Q4"), private per user; Admin can publish a view to everyone. Stored as the URL query string plus a name.
* **Inline edit** for quick fields (stage, owner, due date, status) directly in the grid cell; full edits stay on the detail page.

### Detail page (the bound single form)

- **Header card**: record name, key fields, owner, status chip, action buttons (Edit, Log activity, Delete, plus type-specific actions such as Convert).
- **Read mode by default.** Edit switches the header card into an `EditForm` in place (Access `AllowEdits`). Save and Cancel; Esc cancels.
- `/x/new` opens the same component in create mode; after save it redirects to `/x/{id}`.
- **Tabs** below the header: related records and child panels (Activities, Notes, Attachments, History). Each tab loads only when first opened.
- Validation: DataAnnotations or FluentValidation on the model, shown per field; server re-validates.
- **Concurrency**: every save sends the `RowVersion` it loaded. On conflict show "Changed by {user} at {time} since you opened it" with Reload (lose mine) or Overwrite (Admin only).
- **Unsaved changes**: leaving a dirty form (nav click, browser back) asks to confirm via `NavigationLock`.
- Not found or no permission returns the 404 page for both, so record ids can't be probed.

* **Live changes**: if another user saves the record you are viewing, read mode refreshes in place; in edit mode a banner says "Changed by Maria just now" and the form is never overwritten. Concurrency still guards the save.
* **Star** button in the header adds the record to favourites.

### Child panel (the subform)

- Component with `RelatedType` + `RelatedId` parameters (or the typed FK, per the open decision), reloaded in `OnParametersSetAsync`.
- Add and edit in a `MudDialog`, so the parent page never navigates away (US2).
- Panels: Contacts (on Account), Opportunities (on Account, Contact), Activities timeline, Notes, Attachments, History.

### Delete

- Soft delete only (`IsActive = 0`), with a confirm dialog that names what is affected ("3 contacts and 2 open opportunities stay linked to this account").
- Deleted records vanish from lists and search via a global query filter; Admin can view and restore them (see open decisions).

* Unique rules apply to active rows only: filtered unique indexes (`WHERE IsActive = 1`), so a deleted record never blocks reusing its name, VAT number or email.

## Page specs

Each page follows the patterns in section 4 unless it says otherwise; only what is specific to the page is listed.

### P1 to P5: Sign-in and My account

Scaffold these from the Identity templates (static server rendering, not interactive), then restyle to match the app.

- **P1 Sign in**: email, password, Remember me, Forgot password link; "Sign in with Microsoft" button added in Phase 5. Lockout after 5 failed attempts for 15 minutes; the message never says which of email or password was wrong. On the public demo only: Sign in as Sales / Manager / Admin buttons instead of credentials.
- **P2 Two-factor code**: 6-digit authenticator code, Remember this device (30 days), link to use a recovery code.
- **P3 Forgot password**: email field; always shows "if the address exists, we sent a link". Needs SMTP (Phase 6); until then, Admin resets passwords from P20.
- **P4 Reset password**: token from the email link, new password twice, policy hint shown under the field.
- **P5 My account**: display name, phone, time zone (default Europe/Athens), change password, set up or reset 2FA (QR code plus recovery codes shown once), linked Microsoft account, theme (light, dark, system), notification preferences (email reminders on or off, which in-app events to receive).

### P6: Dashboard

One screen that answers "what do I do today and how is the pipeline". Tiles respect the same Mine / My team / All scope switch as lists.

- **Row 1, KPI tiles**: Open pipeline value (sum of Amount, open stages), Weighted pipeline (Amount x Probability), Won this month (count and value), Lost this month, Overdue tasks (mine).
- **Row 2**: Pipeline by stage (bar chart, value per stage, click a bar to open P14 filtered to that stage); My tasks today and overdue (top 10, tick to complete in place, link to P17).
- **Row 3**: Opportunities closing in the next 30 days (top 10 by close date); Recent activity on my records (last 10 audit entries).
- **Manager extra**: Team leaderboard (won value this month per user).
- Each tile is its own component loading in parallel, with its own skeleton loader, so one slow query doesn't block the page.
- Phone: KPI tiles 2 per row, then My tasks first, chart last.

### P7: Search results

- Query from the top bar or `?q=`. Results grouped by type (Accounts, Contacts, Opportunities, Leads), up to 20 per group with "See all in list" opening that list with the quick filter set.
- Matches: names, email, phone, VAT number. Accent- and case-insensitive ("Αθηνα" finds "Αθήνα") via the D3 collation (Greek\_100\_CI\_AI) on those columns.
- Under 1 second at seed volume (US1). Only records the user may see.
- Recommendation: prefix searches (`LIKE 'term%'`) use the index on the collated columns; move to SQL Server full-text search only if the 1 second target fails.

### P8: Accounts list

- Columns: Name, Industry, City (billing), Status, Owner, Open opportunities (count), Last activity date.
- Filters: Status, Industry, Owner, City.
- Bulk: Reassign owner, Export.
- Counts and last activity come from a projection (`Select` into a DTO with subqueries), not by loading child collections.

### P9: Account detail / new

- **Header fields**: Name (required, unique among active accounts, warned not blocked if a similar name exists), VAT number (unique when filled), Industry, Status, Phone, Website, Owner (defaults to current user).
- **Tabs**: Overview (billing and shipping addresses, inline edit), Contacts, Opportunities, Activities, Notes, Attachments, History.
- **Contacts tab**: grid of contacts with Add contact in a dialog that pre-fills the account (US2); the account is created first, so in create mode this tab appears after the first save.
- **Opportunities tab**: open first, then won and lost; + New opportunity pre-fills the account.
- **Actions**: Edit, Log activity, New opportunity, Delete, Reassign (Manager, Admin).
- **Rule**: Status Inactive does not hide the account; only soft delete hides it.

### P10: Contacts list

- Columns: Full name, Account, Job title, Email, Phone, Owner, Last activity.
- Email and phone are links (`mailto:`, `tel:`): on a phone this is the one-tap call flow.
- Filters: Account, Owner, Has email.

### P11: Contact detail / new

- **Header fields**: First name, Last name (required), Account (autocomplete, required in MVP), Job title, Email (validated, duplicate warning across active contacts), Phone, Mobile, Owner (defaults to the account owner).
- **Tabs**: Activities, Opportunities (of the account), Notes, Attachments, History.
- **Actions**: Edit, Call (phone only, `tel:`), Email (`mailto:`), Log activity, Delete; Admin extra: Erase person (GDPR: hard delete that also scrubs the person's values from existing audit entries, then writes one audit entry that keeps no personal data).
- Phone layout: name, account, then large Call / Email / Log activity buttons, then the activity timeline. This is the core mobile flow.

### P12: Leads list

- Columns: Name, Company, Email, Source, Status, Owner, Created.
- Default filter: Status not Converted and not Disqualified. Converted leads stay queryable for source reporting.
- Statuses (lookup): New, Contacted, Qualified, Disqualified, Converted. Converted is set only by the Convert action.

### P13: Lead detail / new + Convert

- **Header fields**: Name, Company, Email, Phone, Source, Status, Owner.
- **Tabs**: Activities, Notes, History.
- **Convert** (button shown when Status is not Converted or Disqualified) opens a dialog:
  - Account: create new (pre-filled from Company) or pick existing (autocomplete; suggests matches by company name and email domain).
  - Contact: create new (pre-filled), or pick an existing contact of the chosen account.
  - Opportunity: optional checkbox, name defaults to "{Company} deal", stage defaults to the first open stage.
  - Confirm runs `LeadService.ConvertAsync` in one transaction (US3): creates or links the records, moves the lead's activities and notes to the new contact, sets Status Converted, ConvertedAt, and links to the created ids.
- After convert: the lead becomes read-only with links to the new account, contact and opportunity.

### P14: Opportunities list

- Columns: Name, Account, Stage, Amount (EUR), Probability, Weighted, Close date, Owner.
- Filters: Stage, Owner, Close date range, Open / Won / Lost.
- Overdue highlight: open opportunities with Close date in the past.
- Footer totals for the filtered set: Amount and Weighted.
- Toggle in the header switches to P15 with the same filters.

### P15: Pipeline board

- One column per open stage (from the Stage lookup, by SortOrder) plus collapsed Won and Lost columns at the end. Column header shows count and total value.
- Card: name, account, amount, close date, owner initials; red edge when the close date has passed.
- **Drag a card** to another column: stage and probability (stage default) update and the audit log records it, without a page reload (US4). Optimistic UI: the card moves at once and snaps back with an error if the save fails or the row version conflicts.
- Drop on Won or Lost opens a small dialog (Lost reason required, actual close date defaults to today).
- Filters: Owner, My team (Manager), Close date range. Cap at 100 cards per column with a "show more" link to P14.
- **Phone**: no drag and drop. Stages become a horizontal swipeable tab strip; each card has a Stage dropdown (US4 acceptance).

* **Live board**: when another user moves, adds or edits a card, it moves on every open board within a second, with a brief highlight. A card you are dragging is never moved under you.

### P16: Opportunity detail / new

- **Header fields**: Name, Account (required), Primary contact (filtered to the account), Stage, Amount, Currency (EUR only in MVP), Probability (defaults from the stage, editable), Close date, Owner, Lost reason (shown only when Lost).
- Stage shown as a clickable stepper across the header (same rules as dragging on P15).
- **Tabs**: Activities, Notes, Attachments, History (shows each stage change with date and user).
- **Rule**: moving to a Won or Lost stage stamps ClosedAt; moving back to an open stage clears it and asks for confirmation.

### P17: My tasks

- Shows only my open activities with a due date, grouped: **Overdue** (oldest first), **Today**, **Tomorrow**, **This week**, **Later** (US5).
- Each row: checkbox to complete, type icon, subject, related record (link), due time.
- Ticking complete stamps DoneAt and offers "Log a follow-up?" (a new task pre-filled for the same record, due in 3 working days).
- Quick add at the top: subject, due date, optional related record.
- Toggle: Show completed (last 7 days).
- Phone: this is a home-screen flow; large tap targets, swipe right to complete.

### P18: Activities

- All activities I may see, not only tasks: calls, meetings, tasks, completed or open.
- Columns: Type, Subject, Related to, Due, Done, Owner.
- Filters: Type, Owner, Open / Done, date range, Related type.
- Manager uses scope My team to review team activity and reassign (US7).

**Log activity dialog** (shared, opened from any detail page, P17 and + New): Type, Subject, Due or occurred at, Duration (meetings), Outcome note, Done checkbox (pre-ticked for calls and meetings logged after the fact), Related record (pre-filled and locked when opened from a record).

### P19: Reports

MVP holds two reports; Power BI covers anything deeper by reading the database with a read-only login.

- **Pipeline PDF** (US8): parameters Owner or Team, Close date range. One page, totals per stage, weighted total, generated with QuestPDF in under 10 seconds. Download, not email, in MVP.
- **Activity summary** (.xlsx): activities per user per type for a date range, via ClosedXML.
- Recommendation: QuestPDF over SSRS for the MVP, because it deploys with the app on Linux; keep SSRS for the IIS practice run if you want to show both.

### P20: Users

- Grid: Name, Email, Role, Team, Active, Last sign-in, 2FA on.
- Add user: name, email, role, team; sends an invite email with a set-password link (Phase 6). Until SMTP works, Admin sets a temporary password that must be changed at first sign-in.
- Actions: Change role or team, Deactivate (never delete; their records keep the owner), Reset 2FA, Unlock.
- **Deactivate flow**: dialog asks to reassign that user's open records (accounts, contacts, leads, open opportunities, open tasks) to another user in one transaction.
- Rule: the last active Admin cannot be demoted or deactivated.

### P21: Teams

- Grid: Team name, Manager, Members count. Dialog to add or edit: name, manager (a user with role Manager), members.
- Rule: each user is in exactly one team; moving a user moves visibility at once (record scope is computed from current team, not stored on the record).

### P22: Lookups

One generic page drives every lookup table, so adding an industry needs no code change (US10).

- Left: list of lookup types (Industry, Lead source, Lead status, Activity type, Account status, Lost reason, Stage; Phase 9 adds Product category, VAT rate, Unit).
- Right: editable grid of that type's values: Name, Sort order, Active. Inline add and edit, drag to reorder.
- Deactivate instead of delete when a value is in use; deactivated values stay on old records but vanish from dropdowns.
- **Stage** has extra columns: Default probability, Is won, Is lost. Rules: exactly one Won and one Lost stage, at least one open stage.

### P23: Import

A 4-step wizard (`MudStepper`) for accounts and contacts (US9).

1. **Upload**: choose entity, download the template, upload .xlsx (max 10 MB, 20,000 rows).
2. **Map columns**: auto-match headers to fields; unmatched columns can be ignored.
3. **Preview**: every row validated (required fields, formats, lookup values exist, duplicates in file and in database). Shows counts (OK, warnings, errors) and a grid of failing rows with the reason. Choice per duplicate rule: skip or update existing.
4. **Confirm**: queued as a Hangfire job; progress bar; result summary with an error report download. Nothing is written before Confirm, and the whole import commits in batches inside a tracked ImportBatch so it can be rolled back.

### P24: Audit log

- Grid: When, User, Action (Create, Update, Delete, Restore, Convert, Login), Entity, Record (link), Changes.
- Expanding a row shows old and new values per field (US12).
- Filters: User, Entity, Action, date range, record id.
- The same data appears per record in each detail page's **History** tab, filtered by the matrix (Sales own, Manager team, Admin all).

* Sign-in, sign-out and failed sign-in entries are written from the Identity sign-in events; the `SaveChanges` interceptor cannot see them.

### P28: API tokens

- Grid: Name, Owner user, Scope (read only, read and write), Created, Last used, Expires.
- Create: name, user it acts as (the token inherits that user's record visibility), scope, expiry (default 90 days). The token is shown once; only a hash is stored.
- Actions: Revoke (immediate), Regenerate. Every API call logs the token name in the audit log.
- Used by Power BI, Power Automate and the later QuickBooks / Xero hand-off.

### P29: Company settings and branding

- Company name, logo (PNG or SVG, max 500 KB), primary and secondary colour, default currency, default time zone, date format.
- Applied to the `MudTheme` at startup and refreshed live for every open session when saved.
- Contrast check on save: a colour that fails WCAG AA against white text is rejected with a suggested darker shade.
- This is what makes each client's deployment look like their own CRM without a code change.

## Shared components

19 components carry most of the UI: 14 below and 5 in the UI and design system section. Build each once, with a bUnit test, before the pages that use it.

| Component | Used on | What it does |
| --- | --- | --- |
| `CrmListPage<TDto>` | P8, P10, P12, P14, P18 | Grid + scope chips + filters + URL state + export + phone card layout |
| `RecordHeader` | all detail pages | Title, status chip, owner, action buttons, read/edit switch |
| `RecordTabs` | all detail pages | Lazy-loaded tabs; collapsible panels on phones |
| `ActivityTimeline` | P9, P11, P13, P16 | Open and done activities for a record, newest first, Log activity button |
| `LogActivityDialog` | detail pages, P17, + New | Shared create/edit form for activities |
| `NotesPanel` | P9, P11, P13, P16 | Add, edit own, list notes (plain text in MVP) |
| `AttachmentsPanel` | P9, P11, P16 | Upload (max 10 MB, whitelist of types), list, download, delete |
| `HistoryPanel` | all detail pages | Audit entries for one record, field-level diff |
| `AccountPicker` / `ContactPicker` | forms, Convert, Import | `MudAutocomplete`, search-as-you-type, 10 results, "create new" option |
| `OwnerPicker` | forms, bulk reassign | Users the current user may assign to (own team for Manager) |
| `LookupSelect` | all forms | `MudSelect` bound to a lookup type; hides inactive values except the current one |
| `ConfirmDialog` | delete, convert, reassign | Consistent wording and destructive-button style |
| `ConcurrencyAlert` | all edit forms | Conflict message with Reload / Overwrite |
| `KpiTile`, `PipelineChart` | P6 | Self-loading dashboard tiles |

Attachments are stored on disk (or Blob storage later) under a generated name, never the uploaded file name; downloads go through an endpoint that checks permission first.

## UI and design system

The app should feel premium through consistency, speed and behaviour, not effects: business users judge a CRM on how fast and clear it is first. One theme, one page anatomy, every state designed.

### Visual layer

- **One custom `MudTheme`** holds every token: palette, typography (Inter or similar), 8 px radius, flat surfaces with subtle borders instead of heavy shadows, comfortable and compact density. No colour or font is set anywhere else.
- **Light and dark mode**, both derived from the theme; per-user choice stored in the profile.
- **Per-client branding** from P29: logo and primary colour come from the database.
- **Consistent page anatomy**: header card, tabs, the same button order and placement on every record type.
- **Every state designed**: skeleton loaders while loading, empty states with one clear next action, friendly error states with a retry.
- **Data-forward details**: status chips with consistent colour meaning, avatar initials for owners, relative dates ("in 3 days") with the exact date on hover, tabular numerals and right alignment for money.

### Blazor behaviours

- **Real-time push** over the existing SignalR circuit: notifications, live board, live record changes.
- **Optimistic UI**: drag, tick and inline edit react instantly and roll back with a message on error.
- **`Virtualize`** for long timelines and phone card lists.
- **Streaming rendering** so the dashboard shell paints at once and tiles fill in.
- **Keyboard first**: Ctrl+K palette, N new, / search, E edit, Esc cancel, Ctrl+S save.
  - Built (8 Oct 2026 plan, Phase 3): `Ctrl+K` / `Cmd+K` opens the command palette (records, pages, actions; empty it lists actions, favourites and recently viewed; Enter opens the top result). `/` jumps to the search box. `E` edits the record on a detail page, `N` opens New on a list page. `Ctrl+S` / `Cmd+S` saves the form being edited (and suppresses the browser's "Save page as"). `Esc` cancels an edit, closes the search list and closes dialogs.
  - Single-letter shortcuts (`/`, `E`, `N`) are ignored while the focus is in an input, textarea or select, or while a dialog or menu is open. `Ctrl`/`Cmd` combinations work everywhere.
  - They match the physical key, so they also work on a Greek keyboard layout. Other layouts (Cyrillic and so on) are not covered yet.
  - Implemented with MudBlazor's `MudKeyInterceptor` on one wrapper element (`#crm-shell` in `MainLayout`); components register what they can do right now with the scoped `ShortcutRegistry`. The key interceptor does not report where a key was pressed, so the focus check is one `document.querySelector` call through the existing Blazor JS interop (`BrowserFocus`); there is no script file of our own.

### Shared components added

| Component | What it does |
| --- | --- |
| `NotificationBell` | Unread count, panel, mark read; subscribes to the user's notification channel |
| `CommandPalette` | Ctrl+K dialog over global search plus a list of actions |
| `LiveRecordBanner` | Shows "changed by X" on a detail page; refreshes read mode |
| `SavedViewMenu` | Save, rename, delete, publish list views |
| `ThemeProvider` | Builds the `MudTheme` from P29 settings and the user's light / dark choice |

### Rules

- Animations and hover effects in CSS only, never driven from C#, or they lag on every round trip.
- Live updates never overwrite a form being edited.
- No second UI library next to MudBlazor; two design languages look cheap.
- Before building pages, record the tokens and components as a MexCrm Design System and mock up the 6 key screens (phone and desktop) from it.

## Performance and speed

In Blazor Server almost all delay is the network round trip on each click, not C#. Targets: list pages under 300 ms server time and screen updates under 100 ms on an office network, checked in CI and on a real phone.

### Hosting (biggest lever)

- **No cold starts**: free tiers sleep and the first load takes 10 to 30 s. UptimeRobot pings every 5 minutes, or a tier with always on, before anyone sees a demo.
- **App and database in the same datacenter**; a cross-provider database adds latency to every query (see D4).
- **WebSockets on**; long-polling fallback slows every click.
- **Host near the users**: EU region for EU clients, US for US. Every 100 ms of distance is added to every interaction.

### Database

- Lists: `AsNoTracking`, `Select` into slim DTOs, server paging, no `Include` of whole graphs.
- An index for every list filter and sort, covering indexes for default list views; verified with Query Store at seed volume.
- Paging count and page query run together; totals for very large unfiltered lists cached.
- Dashboard KPIs cached 60 s with `HybridCache`, invalidated on relevant saves.
- Lookups, stages, settings, user team and role cached in memory, refreshed when an admin edits them.

### Rendering

- `@key` on repeated items, `ShouldRender` on heavy components, no `StateHasChanged` in loops.
- Search debounced 250 ms; the previous query cancelled with a `CancellationToken`.
- Light grid cells (text and chips, not a full component per cell): nested components per row are the usual cause of a slow `MudDataGrid`.
- `Virtualize` above about 50 visible rows; tabs lazy-loaded; dashboard tiles load in parallel.

### Perceived speed

- Optimistic UI for drag, tick and inline edit.
- Skeletons instead of spinners; page shell streamed first.
- Prerender off for interactive pages (no double data loads); the reconnect overlay covers the connect moment.
- Anything over about 1 s (import, export, PDF, bulk reassign) runs in Hangfire and notifies when done.

### Payload

- Self-hosted woff2 fonts, fingerprinted static assets (`MapStaticAssets`), Brotli compression, no extra JavaScript libraries.

### Budgets

| Measure | Target |
| --- | --- |
| Server time, list and search pages (p95, seed data) | under 300 ms |
| Click to screen update, office network | under 100 ms |
| Click to screen update, phone on 4G | under 300 ms |
| First load, warm server | under 2 s |
| Dashboard fully filled | under 1.5 s |

Measured with EF Core SQL logging and Query Store in development, OpenTelemetry metrics and `dotnet-counters` on the server, and throttled Playwright runs in Phase 8. The 300 ms target is tighter than the Rev 2 plan's 500 ms NFR.

## Workflows and business rules

Every rule below lives in a service in `Crm.Core`, never in a component, so the UI, import, export and API all obey it.

&#91;embedded content: record lifecycle · lead to won or lost\]

A lead either converts (creating or linking up to three records in one transaction) or is disqualified; only the opportunity moves on to Won or Lost.

| Rule | Detail | Service |
| --- | --- | --- |
| Record visibility | Sales: own records, read-only on team records. Manager: own team edit, other teams read. Admin: all. Applied by one `VisibleTo(user)` query extension used by lists, search, export, dashboard and API. | `AccessService` |
| Ownership | New records default to the creator. Contacts default to the account owner. Reassigning an account offers to cascade to its contacts and open opportunities (checkbox, default on). | `OwnershipService` |
| Lead conversion | One transaction: account (new or existing), contact (new or existing), optional opportunity; lead activities and notes move to the contact; lead marked Converted, never deleted. | `LeadService.ConvertAsync` |
| Stage change | Probability resets to the stage default unless the user edited it on this record. Won or Lost stamps ClosedAt; Lost requires a reason. Every change is audited. | `OpportunityService.MoveStageAsync` |
| Overdue | An activity is overdue when DueAt < now (UTC) and DoneAt is null. An opportunity is overdue when open and CloseDate < today (Athens). | query helpers |
| Task reminders | Hangfire job at 08:00 Europe/Athens emails each user their tasks due tomorrow; a ReminderSentAt stamp prevents duplicates on job retry. | `ReminderJob` |
| Soft delete | Account delete is blocked while it has open opportunities; otherwise its contacts are soft-deleted with it, and restore brings them back together. | per entity service |
| Duplicates | Warn, don't block: same account name, VAT number, or contact email among active records. Import applies the same check. | `DuplicateService` |
| Audit | A `SaveChanges` interceptor writes Create, Update, Delete with field-level old and new values; skips RowVersion and the audit columns themselves. | `AuditInterceptor` |
| Time | Stored in UTC, shown in the user's time zone (default Europe/Athens); date-only fields (CloseDate) stored as `date`, no time zone. | display helpers |
| Demo mode | Public demo only (a setting, off for client deployments): one-click sign-in per role, a banner saying the data is fake and resets daily, a nightly Hangfire job that restores the seed, uploads and outgoing email disabled. | DemoService |

## Build order

Pages slot into the Rev 2 phases; within a phase, build the shared component first, then the simplest page that uses it.

| Phase | Pages | Build first |
| --- | --- | --- |
| 2. Foundation | Shell, P1, system pages, an empty P6 | `MainLayout`, nav by role, Identity with seeded Admin / Manager / Sales users, `VisibleTo` stub |
| 3. Core UI | P8, P9, P10, P11, P7 | `CrmListPage`, `RecordHeader`, `RecordTabs`, `AccountPicker`, `ConcurrencyAlert`, search column |
| 4. Pipeline and activities | P12 to P18 | `ActivityTimeline`, `LogActivityDialog`, `NotesPanel`, `AttachmentsPanel`, `LeadService.ConvertAsync`, `MoveStageAsync` |
| 5. Security, data, API | P2, P5, P20 to P24, P28, P29, History tabs | Real `VisibleTo` rules + tests per matrix row, `AuditInterceptor`, `HistoryPanel`, generic lookup grid, import job |
| 6. Reporting and email | P6 tiles, P19, P3, P4 | `KpiTile`, `PipelineChart`, QuestPDF report, SMTP + reminder job |

Features added on 8 Oct 2026 (about 22 hours, inside the plan's slack): saved views and recently viewed in Phase 3; live board and live record changes in Phase 4; P28 API tokens and P29 settings and branding in Phase 5; notifications bell in Phase 6; demo mode and nightly reset in Phase 7. Ctrl+K palette and dark mode ride along with the shell in Phase 3.

Suggested first vertical slice inside Phase 3: P8 Accounts list plus P9 Account detail with only the header (no tabs). It exercises the list pattern, the detail pattern, validation, concurrency and URL state on one entity before you copy them.

## Post-MVP: Products and Quotes

A Phase 9 module of about 25 to 30 hours, started only after the MVP is complete. Products is the "new CRUD module in under 4 hours" test from objective 1; Quotes adds master-detail line editing, a PDF document and a status workflow. Invoices and orders stay out (D10).

### P25: Products list

- Columns: Code, Name, Category, Unit price (EUR), VAT rate, Active.
- Filters: Category, Active. Admin and Manager edit; Sales read only.

### P26: Product detail / new

- Fields: Code (required, unique), Name, Description, Category (lookup), Unit price, VAT rate (lookup), Unit (lookup: piece, hour, month), Active.
- Deactivate instead of delete once a product appears on any quote.

### P27: Quote editor

- Route `/quotes/{id}`, `/opportunities/{id}/quotes/new`. A quote always belongs to an opportunity, and through it to an account.
- **Header**: Number (`Q-2026-0001` from a SQL Server sequence, not the identity column), Revision (A, B, ...), Contact, Issue date, Valid until (default +30 days), Status, Notes and terms.
- **Lines grid** (the subform): Product (autocomplete) or free-text line, Description, Qty, Unit price, Discount %, VAT rate, Line net, Line VAT, Line total. Add, reorder and delete rows inline.
- **Totals**: net, VAT per rate, grand total. Money as `decimal(18,2)`, rounded per line, then summed.
- **Snapshot rule**: each line copies product name, unit price and VAT rate at the time it is added; later product price changes never alter existing quotes.
- **Statuses**: Draft, Sent, Accepted, Rejected, Expired. Only Draft is editable. Editing a Sent quote creates the next revision and marks the previous one Superseded.
- **Actions**: Download PDF (QuestPDF), Mark sent, Accept, Reject. Accept is allowed for one quote per opportunity; it sets the opportunity Amount to the quote net total and moves it to Won.
- Expiry: a daily Hangfire job marks Sent quotes past Valid until as Expired.

### Quotes tab on P16 Opportunity detail

- List of the opportunity's quotes with number, revision, status, total, valid until; + New quote.

### Later option: accounting hand-off

- Accepted quote pushes a draft invoice to QuickBooks Online or Xero through their API (both offer free developer sandboxes). The CRM never issues invoices itself.

### Other post-MVP items

- Activity calendar view, week and month (Heron.MudCalendar, MIT licence).
- Merge duplicate accounts and contacts (Admin), moving all children to the survivor.
- Web-to-lead: a public form endpoint that creates leads, with a honeypot field and rate limit.
- Parent and child account hierarchy.
- GDPR data export for a person, paired with the existing Erase.

## Open decisions and edge cases

Three decisions block Phase 2 because they shape the schema: links, data model gaps and collation. The rest can wait for their phase.

| # | Decision | Recommendation | Needed by |
| --- | --- | --- | --- |
| D1 | Activity, Note, Attachment links: polymorphic or typed FKs | Decided 8 Oct: typed nullable FKs (AccountId, ContactId, OpportunityId, LeadId) + a check constraint that exactly one is set. Real referential integrity, simpler LINQ. | Phase 2 |
| D2 | Data model gaps found while writing this spec | Add: Contact.OwnerId, Contact.Mobile; Lead.OwnerId, Lead.Phone, Lead.ConvertedAccountId / ContactId / OpportunityId; Opportunity.PrimaryContactId, LostReasonId, ClosedAt; Activity.DurationMinutes, ReminderSentAt; lookups LeadStatus and LostReason. New tables: Notification, SavedView, Favourite, RecentView, ApiToken, CompanySetting, ImportBatch (with ImportBatchId on Account and Contact for import rollback). Full definitions in the Data dictionary tab. | Phase 2 |
| D3 | Accent-insensitive search | `Greek_100_CI_AI` collation on the searchable columns, set in the EF configuration. Verify "Αθηνα" finds "Αθήνα" in a test before building P7. | Phase 2 |
| D4 | Hosting | Decided 8 Oct: MonsterASP (IIS, SQL Server in the same datacenter) for staging and demo; Docker/Linux optional. Verify 5 checks: app stays alive (no cold starts, also needed for the 08:00 Hangfire job), WebSockets on, app and database in the same datacenter, EU region, disk quota fits attachments. Also confirm custom domain mapping and the database size cap; if the 50k seed does not fit, staging uses a smaller seed. | Phase 2 |
| D5 | Contact without an account | Required in MVP (B2B). Revisit if a client needs B2C. | Phase 3 |
| D6 | Restoring deleted records | No recycle-bin page: a "Show deleted" toggle on lists for Admin, with Restore on the record. | Phase 5 |
| D7 | Linking a lead to an account owned by a teammate | Allowed when visible (read-only); the new contact and opportunity are owned by the converting user. | Phase 4 |
| D8 | Reports for Sales | Manager and Admin only, per US8. | Phase 6 |
| D9 | Currency | EUR only; keep the Currency column for later. | Phase 4 |
| D10 | Invoices and orders | Out of scope. Accepted quotes hand off to the client's accounting system (QuickBooks / Xero API integration as a later option). | Phase 9 |

**Edge cases to test**

- Same record open in two tabs or by two users: second save gets the concurrency dialog, not a silent overwrite.
- Owner deactivated: their records keep the owner, shown as "(inactive)" until reassigned; owner pickers hide inactive users.
- Deactivated lookup value still on a record: `LookupSelect` shows it on that record only.
- Direct URL to a record the user can't see: 404, same as a missing id.
- Prerendering runs `OnInitializedAsync` twice: prerender is off for interactive pages (see Performance and speed); verify no page loads data twice.
- Reminder job across daylight saving changes: register the Hangfire recurring job with the Europe/Athens time zone, not a UTC cron.
- Large import or export on a phone or slow link: both run as jobs with a download link, so a dropped circuit loses nothing.
- Pipeline drag while another user moved the same card: the card snaps back with the conflict message and the board refreshes that column.

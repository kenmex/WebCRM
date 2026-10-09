# Data dictionary

Every MVP table and column, as EF Core will create it in SQL Server. Phase 2 entities and configurations are written from this tab; change it here first, then in code.

## Conventions

- **Keys**: `Id int IDENTITY` primary key on every table. FKs are `<Entity>Id`, `DeleteBehavior.Restrict` unless stated.
- **Users**: user FKs (OwnerId, CreatedBy, UpdatedBy) are `nvarchar(450)`, the ASP.NET Core Identity key type, FK to `AspNetUsers`.
- **Base columns** on every business table (marked *base*): CreatedAt `datetime2(0)` not null default `sysutcdatetime()`; CreatedBy `nvarchar(450)` not null; UpdatedAt `datetime2(0)` null; UpdatedBy `nvarchar(450)` null; IsActive `bit` not null default 1 (soft delete); RowVersion `rowversion`. Set by the `SaveChanges` interceptor.
- **Times** in UTC as `datetime2(0)`; date-only fields as `date`.
- **Money** `decimal(18,2)`; percentages `decimal(5,2)`.
- **Search columns** use collation `Greek_100_CI_AI` (marked AI).
- **Uniqueness** on soft-deleted tables uses filtered unique indexes `WHERE IsActive = 1`.
- **Strings** are `nvarchar` with the length shown; `nvarchar(max)` only where stated.

## Accounts and contacts

**Account** (*base*)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Name | nvarchar(200) AI | no |  | Unique among active accounts; similar names warned |
| VatNumber | nvarchar(20) | yes |  | Shown as "VAT / Tax ID"; works for any country. Stored normalised: upper case, no spaces, dots or dashes; 4 to 20 letters or digits (check constraint `CK_Accounts_VatNumber`, binary collation so lower case is rejected). No prefix rule, except `EL` (Greece): exactly 9 digits and a correct ΑΦΜ check digit (first 8 digits weighted 256 down to 2, sum mod 11 mod 10 = 9th digit; 000000000 rejected; the check digit is checked in code, the constraint checks the shape). A Greek-letter `ΕΛ` prefix is stored as `EL`. A bare 9-digit number becomes `EL` + digits only when `CompanySetting.DefaultCountryCode` is `GR`; otherwise it is stored as typed (a US EIN is 9 digits too). Unique when filled, among active accounts (compared after normalising) |
| IndustryId | int | yes |  | FK Industry |
| AccountStatusId | int | no | first status | FK AccountStatus |
| Phone | nvarchar(30) | yes |  |  |
| Website | nvarchar(300) | yes |  | Valid absolute http or https URL. A missing scheme is not an error: `https://` is added on save (`mexdb.com` is stored as `https://mexdb.com`) |
| OwnerId | nvarchar(450) | no | creator | FK user; index (OwnerId, IsActive) |
| ImportBatchId | int | yes |  | FK ImportBatch, for import rollback |

**Address**

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| AccountId | int | no |  | FK Account |
| AddressType | tinyint | no | 1 | 1 billing, 2 shipping; unique (AccountId, AddressType) |
| Street | nvarchar(200) | yes |  |  |
| City | nvarchar(100) AI | yes |  | Indexed for list filter |
| Postcode | nvarchar(20) | yes |  |  |
| CountryCode | char(2) | yes | company default | ISO 3166-1 alpha-2 |

**Contact** (*base*)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| FirstName | nvarchar(100) AI | yes |  |  |
| LastName | nvarchar(100) AI | no |  |  |
| FullName | computed, persisted |  |  | `CONCAT_WS(' ', FirstName, LastName)`; indexed for search and sort |
| AccountId | int | no |  | FK Account (required in MVP, D5) |
| JobTitle | nvarchar(100) | yes |  |  |
| Email | nvarchar(254) AI | yes |  | Valid format; duplicates among active contacts warned; indexed |
| Phone | nvarchar(30) | yes |  |  |
| Mobile | nvarchar(30) | yes |  |  |
| OwnerId | nvarchar(450) | no | account owner | FK user; index (OwnerId, IsActive) |
| ImportBatchId | int | yes |  | FK ImportBatch |

## Sales pipeline

**Lead** (*base*)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Name | nvarchar(200) AI | no |  | Person's name |
| Company | nvarchar(200) AI | yes |  | Pre-fills the account on Convert |
| Email | nvarchar(254) AI | yes |  | Valid format |
| Phone | nvarchar(30) | yes |  |  |
| LeadSourceId | int | yes |  | FK LeadSource |
| LeadStatusId | int | no | New | FK LeadStatus; Converted only via Convert |
| OwnerId | nvarchar(450) | no | creator | FK user |
| ConvertedAt | datetime2(0) | yes |  | Set by Convert; lead read-only once set |
| ConvertedAccountId | int | yes |  | FK Account |
| ConvertedContactId | int | yes |  | FK Contact |
| ConvertedOpportunityId | int | yes |  | FK Opportunity |

**Opportunity** (*base*)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Name | nvarchar(200) AI | no |  |  |
| AccountId | int | no |  | FK Account |
| PrimaryContactId | int | yes |  | FK Contact; must belong to the same account (service rule) |
| StageId | int | no | first open stage | FK Stage |
| Amount | decimal(18,2) | no | 0 | Check >= 0 |
| Currency | char(3) | no | EUR | EUR only in MVP |
| Probability | decimal(5,2) | no | stage default | Check 0 to 100 |
| ProbabilityOverridden | bit | no | 0 | Set when the user edits Probability; stage changes then keep it |
| CloseDate | date | no | today + 30 | Expected close |
| ClosedAt | datetime2(0) | yes |  | Stamped on Won or Lost, cleared on reopen |
| LostReasonId | int | yes |  | FK LostReason; required when the stage is Lost (service rule) |
| OwnerId | nvarchar(450) | no | creator | FK user |

Indexes: (StageId, OwnerId, IsActive) for the board; (CloseDate) for closing-soon and overdue.

**Stage** (lookup)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Name | nvarchar(100) | no |  | Unique |
| SortOrder | int | no |  | Board column order |
| DefaultProbability | decimal(5,2) | no | 0 | Check 0 to 100 |
| IsWon | bit | no | 0 | Filtered unique index: exactly one Won stage |
| IsLost | bit | no | 0 | Filtered unique index: exactly one Lost stage; check not both |
| IsActive | bit | no | 1 |  |

## Activities, notes and attachments

All three use typed nullable links (D1) with a check constraint that exactly one link is set, written as `(CASE WHEN AccountId IS NULL THEN 0 ELSE 1 END + ...) = 1`. Each link column is indexed.

**Activity** (*base*)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| ActivityTypeId | int | no |  | FK ActivityType (call, meeting, task) |
| Subject | nvarchar(200) | no |  |  |
| Description | nvarchar(max) | yes |  | Outcome note |
| DueAt | datetime2(0) | yes |  | UTC; tasks need it, logged calls may not |
| DoneAt | datetime2(0) | yes |  | Null = open |
| DurationMinutes | int | yes |  | Check > 0; meetings |
| OwnerId | nvarchar(450) | no | creator | FK user |
| AccountId, ContactId, OpportunityId, LeadId | int | yes |  | Exactly one set |
| ReminderSentAt | datetime2(0) | yes |  | Stops duplicate reminder emails |

Index: (OwnerId, DoneAt, DueAt) for My tasks and overdue counts.

**Note** (*base*)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Body | nvarchar(4000) | no |  | Plain text |
| AccountId, ContactId, OpportunityId, LeadId | int | yes |  | Exactly one set |

**Attachment** (*base*)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| StoredName | nvarchar(100) | no |  | Generated GUID + extension; unique; never the uploaded name |
| OriginalName | nvarchar(255) | no |  | Shown and used on download |
| ContentType | nvarchar(100) | no |  | From the allowed-types list |
| SizeBytes | bigint | no |  | Check <= 10,485,760 (10 MB) |
| AccountId, ContactId, OpportunityId | int | yes |  | Exactly one set |

## Users, teams and lookups

**User** (extends `AspNetUsers`; roles in `AspNetRoles`: Admin, Manager, Sales)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| DisplayName | nvarchar(100) AI | no |  |  |
| TeamId | int | yes |  | FK Team; one team per user |
| PhoneNumber | nvarchar(30) | yes |  | Identity column |
| TimeZoneId | nvarchar(64) | no | company default | IANA id, e.g. Europe/Athens |
| Theme | tinyint | no | 0 | 0 system, 1 light, 2 dark |
| EmailReminders | bit | no | 1 | Daily task reminder email |
| NotifyAssigned, NotifyTaskDue, NotifyRecordChanged | bit | no | 1 | In-app notification preferences |
| IsActive | bit | no | 1 | Deactivated users cannot sign in and leave owner pickers |
| MustChangePassword | bit | no | 0 | Set when an Admin sets a temporary password |
| LastSignInAt | datetime2(0) | yes |  |  |

**Team** (*base*)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Name | nvarchar(100) | no |  | Unique among active teams |
| ManagerId | nvarchar(450) | yes |  | FK user with role Manager |

**Lookups**: Industry, LeadSource, LeadStatus, ActivityType, AccountStatus, LostReason (Stage is above)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Name | nvarchar(100) | no |  | Unique per table |
| SortOrder | int | no | 0 | Dropdown order |
| IsActive | bit | no | 1 | Inactive values stay on old records, hidden from dropdowns |
| SystemCode | nvarchar(30) | yes |  | Unique when set; values the code relies on (LeadStatus NEW, CONVERTED, DISQUALIFIED; ActivityType TASK, CALL, MEETING); Admin can rename but not deactivate them |

## System tables

**AuditLog** (append only; no base columns)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Id | bigint IDENTITY | no |  |  |
| UserId | nvarchar(450) | yes |  | Null for background jobs |
| Action | nvarchar(20) | no |  | Create, Update, Delete, Restore, Convert, Erase, SignIn, SignOut, SignInFailed |
| EntityName | nvarchar(50) | yes |  | Null for sign-in events |
| EntityId | int | yes |  |  |
| Changes | nvarchar(max) | yes |  | JSON of field old and new values; check `ISJSON(Changes) = 1`; scrubbed on Erase |
| Source | nvarchar(50) | no | UI | UI, API token name, Import, Job |
| ChangedAt | datetime2(0) | no | sysutcdatetime() |  |

Indexes: (EntityName, EntityId, ChangedAt) for History tabs; (UserId, ChangedAt).

**Notification**

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Id | bigint IDENTITY | no |  |  |
| UserId | nvarchar(450) | no |  | Recipient |
| Type | nvarchar(30) | no |  | Assigned, TaskDue, RecordChanged, JobDone |
| Title | nvarchar(200) | no |  |  |
| Link | nvarchar(300) | yes |  | Relative URL of the record |
| CreatedAt | datetime2(0) | no | sysutcdatetime() |  |
| ReadAt | datetime2(0) | yes |  |  |

Index: (UserId, ReadAt, CreatedAt). A nightly job deletes read notifications older than 90 days.

**SavedView**

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| UserId | nvarchar(450) | no |  | Owner |
| ListKey | nvarchar(50) | no |  | accounts, contacts, leads, opportunities, activities |
| Name | nvarchar(100) | no |  | Unique (UserId, ListKey, Name) |
| QueryString | nvarchar(2000) | no |  | Filters, sort, columns, scope |
| IsPublic | bit | no | 0 | Only Admin can set it |

**Favourite** and **RecentView**

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| UserId | nvarchar(450) | no |  |  |
| EntityName | nvarchar(50) | no |  |  |
| EntityId | int | no |  | Unique (UserId, EntityName, EntityId) |
| CreatedAt / ViewedAt | datetime2(0) | no | sysutcdatetime() | RecentView keeps the last 50 per user, upserted on view |

**ApiToken**

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Name | nvarchar(100) | no |  | Unique among unrevoked tokens |
| UserId | nvarchar(450) | no |  | The user the token acts as; inherits their visibility |
| TokenHash | binary(32) | no |  | SHA-256 of the token; unique; the token itself is never stored |
| TokenPrefix | char(8) | no |  | First characters, shown to identify the token |
| Scope | tinyint | no | 1 | 1 read, 2 read and write |
| ExpiresAt | datetime2(0) | no | +90 days |  |
| LastUsedAt | datetime2(0) | yes |  | Updated at most once a minute |
| RevokedAt | datetime2(0) | yes |  |  |
| CreatedAt, CreatedBy |  | no |  |  |

**CompanySetting** (single row: check `Id = 1`)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| CompanyName | nvarchar(200) | no |  |  |
| LogoData | varbinary(max) | yes |  | PNG or SVG, max 500 KB; SVG sanitized on upload |
| LogoContentType | nvarchar(50) | yes |  |  |
| PrimaryColor, SecondaryColor | char(7) | no | theme defaults | #RRGGBB; WCAG AA contrast check on save |
| DefaultCurrency | char(3) | no | EUR |  |
| DefaultTimeZoneId | nvarchar(64) | no | Europe/Athens |  |
| DefaultCountryCode | char(2) | yes |  |  |
| DateFormat | nvarchar(20) | no | dd/MM/yyyy |  |
| DemoMode | bit | no | 0 | On only for the public demo |
| UpdatedAt, UpdatedBy, RowVersion |  |  |  |  |

**ImportBatch**

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Entity | nvarchar(20) | no |  | Account or Contact |
| FileName | nvarchar(255) | no |  |  |
| Status | tinyint | no | 0 | 0 queued, 1 running, 2 completed, 3 failed, 4 rolled back |
| DuplicateRule | tinyint | no | 0 | 0 skip, 1 update existing |
| TotalRows, OkRows, WarningRows, ErrorRows | int | no | 0 |  |
| ErrorReportName | nvarchar(100) | yes |  | Stored file, same rules as attachments |
| StartedAt, CompletedAt | datetime2(0) | yes |  |  |
| CreatedAt, CreatedBy |  | no |  |  |

Rollback removes rows the batch inserted (by ImportBatchId); rows it updated are restored from the audit log, so updates carry Source = Import.

Tables not in EF Core: the Identity tables beyond the User columns above, and Hangfire's own tables in the `HangFire` schema (created by Hangfire at startup).

## Phase 9: Products and Quotes (post-MVP)

**Product** (*base*)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Code | nvarchar(50) | no |  | Unique among active products |
| Name | nvarchar(200) AI | no |  |  |
| Description | nvarchar(1000) | yes |  | Copied to quote lines |
| ProductCategoryId | int | yes |  | FK ProductCategory (lookup) |
| UnitPrice | decimal(18,2) | no | 0 | Check >= 0 |
| VatRateId | int | no |  | FK VatRate |
| UnitId | int | no |  | FK Unit (lookup: piece, hour, month) |

**VatRate** (lookup) adds Rate `decimal(5,2)`, check 0 to 100.

**Quote** (*base*)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| Number | nvarchar(20) | no |  | `Q-{year}-{0000}` from SQL Server sequence `QuoteNumberSeq` |
| Revision | char(1) | no | A | Unique (Number, Revision) |
| OpportunityId | int | no |  | FK Opportunity |
| ContactId | int | yes |  | FK Contact of the same account |
| IssueDate | date | no | today |  |
| ValidUntil | date | no | IssueDate + 30 | Check >= IssueDate |
| Status | tinyint | no | 0 | 0 draft, 1 sent, 2 accepted, 3 rejected, 4 expired, 5 superseded; only draft is editable |
| NetTotal, VatTotal, GrandTotal | decimal(18,2) | no | 0 | Recalculated from lines on every save |
| Notes | nvarchar(4000) | yes |  | Terms printed on the PDF |
| AcceptedAt | datetime2(0) | yes |  |  |

Filtered unique index: one accepted quote per opportunity (`WHERE Status = 2`).

**QuoteLine** (lines belong to the quote: cascade delete)

| Column | Type | Null | Default | Rules |
| --- | --- | --- | --- | --- |
| QuoteId | int | no |  | FK Quote |
| LineNo | int | no |  | Unique (QuoteId, LineNo); drag to reorder |
| ProductId | int | yes |  | FK Product; null for a free-text line |
| Description | nvarchar(500) | no |  | Snapshot of the product name and description |
| Qty | decimal(18,3) | no | 1 | Check > 0 |
| UnitPrice | decimal(18,2) | no |  | Snapshot; check >= 0 |
| DiscountPct | decimal(5,2) | no | 0 | Check 0 to 100 |
| VatRate | decimal(5,2) | no |  | Snapshot of the rate, not a FK |
| LineNet, LineVat, LineTotal | decimal(18,2) | no |  | Rounded per line in the service, then summed into the quote |

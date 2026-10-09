# Performance baseline on the demo data

Measured 2026-10-09 with `dotnet run --project src/WebCRM.DemoData -- measure` on the developer machine.

- **Data:** the default demo set (10,000 accounts, 50,000 contacts, 15,000 opportunities, 4,000 leads, 200,000 activities), written by `seed`.
- **Machine:** SQL Server 2025 Developer on the same machine, 8 logical cores, 31 GB RAM, default instance settings.
- **What is timed:** the service call the page makes (`AccountService.SearchAsync`, `ContactService.SearchAsync`, `SearchService.SearchAsync`): SQL plus EF, with the count and the page query running together, as the app does. It does not include Blazor rendering or the network, which come on top.
- **Method:** 3 warm-up runs, then 20 timed runs per scenario; the budget is compared with the **p95**. Budgets: 300 ms for list pages, 1 s for global search (`docs/page-spec.md`, Performance and speed).

Result: **50 of 70 scenarios are within budget, 20 are over.** Everything in the Accounts and Contacts lists that does not use the quick-filter box is fast: default views, every typed filter, deep pages and almost every sort are at or under about 210 ms, most under 100 ms.

## Over budget

| Group | Scenario | Rows | p95 ms | Budget ms |
| --- | --- | ---: | ---: | ---: |
| Accounts | Quick filter: Greek word, no accents | 188 | 415 | 300 |
| Accounts | Quick filter: `Α.Ε.` | 1,094 | 388 | 300 |
| Accounts | Sort Last activity desc, page 200 | 10,001 | 342 | 300 |
| Contacts | Quick filter: Greek surname, no accents | 79 | 1,283 | 300 |
| Contacts | Quick filter: email fragment | 4 | 1,574 | 300 |
| Contacts | Quick filter: `an` | 32,455 | 713 | 300 |
| Contacts | Sort Owner asc / desc | 50,002 | 318 / 459 | 300 |
| Contacts | Sort Last activity asc / desc | 50,002 | 383 / 306 | 300 |
| Contacts | Sort Last activity desc, page 1000 | 50,002 | 453 | 300 |
| Search | Top bar, Greek company word | 188 | 1,700 | 1,000 |
| Search | Results page, Greek company word | 188 | 1,456 | 1,000 |
| Search | Top bar, English company word | 13 | 1,385 | 1,000 |
| Search | Top bar, Greek surname | 86 | 1,329 | 1,000 |
| Search | Results page, Greek surname | 86 | 1,014 | 1,000 |
| Search | Top bar / results page, phone fragment | 1 | 1,353 / 1,176 | 1,000 |
| Search | Results page, `an` | 24,171 | 1,005 | 1,000 |
| Search | Results page, no match at all | 0 | 1,210 | 1,000 |

## Why: the time is the substring scan, not a missing index

A search such as `%term%` cannot use an index, so it reads every row, and with the Greek accent-insensitive collation (`Greek_100_CI_AI`, needed for D3) each comparison is expensive. Timed in SQL Server directly, on the 50,000 contacts:

| One `LIKE '%zzqx%'` scan over | CPU ms |
| --- | ---: |
| FullName (Greek_100_CI_AI) | 156 |
| Email (Greek_100_CI_AI) | 219 |
| JobTitle / Phone / Mobile | 94 / 93 / 93 |
| Account name (10,000 rows) | 32 |
| **All six predicates, as the app runs them** | **844 to 906** |
| The same text pre-folded into one key, binary collation | **31 to 35** |

A contact search runs those six predicates twice (once to count, once to fetch the page), and global search runs the account and contact versions, so about 1 s of CPU is the floor. The binary-collation row is 28 times faster and does not depend on how many rows match.

An index does not help. A composite `(ContactId, DoneAt)` index for the Last activity sorts was tested on a temporary copy and made no difference (292 ms with the existing `ContactId` index, 323 ms with the composite): the sort has to compute a maximum for every one of the 50,000 rows.

## Options (none applied yet)

1. **A pre-folded search key column** on Accounts and Contacts (lower case, accents removed, final sigma folded, binary collation), kept up to date by the services on save, searched with one binary `LIKE`. About 30 to 40 ms per scan at this volume. Needs a migration with a back-fill, a data-dictionary change, and a test that the C# folding agrees with the database collation across the Greek alphabet. Keeps substring matching.
2. **One query for count and page** (`COUNT(*) OVER()`). Measured 328 ms elapsed here because SQL Server went parallel (1.7 s of CPU). It would not help on a host with few cores.
3. **Search only the columns that fit the text** (an `@` means email, digits mean phone and tax id, otherwise names). Names only is still about 500 ms, so this is not enough alone.
4. **Prefix matching** (`LIKE 'term%'`), which the spec recommends and which uses the existing indexes. It changes behaviour: "smith" would no longer find "John Smith" by the surname alone.
5. **SQL Server full-text search**, as the spec suggests if the 1 s target fails. Matches whole words and prefixes, not substrings.
6. **Last activity and Owner sorts**: marginal (300 to 460 ms). A stored `LastActivityAt` on the record, maintained when an activity is logged or completed, would fix the Last activity ones; leave it until Phase 4 builds activities.

# Running the tests

## Everything that is not timing based (default, local and CI)

```
dotnet test
```

One project or one class:

```
dotnet test --project tests/WebCRM.Data.Tests --filter-class "*PersonalServicesTests"
```

The SQL Server integration tests use a throwaway database on the server named by `WEBCRM_TEST_SQLSERVER` and skip
themselves when it is unreachable (set `WEBCRM_TEST_REQUIRE_SQLSERVER=1` to make that a failure instead, as CI does).

## Performance tests (on demand)

Tests that assert on elapsed time carry `[Trait("Category", "Performance")]`. They depend on the machine and its load,
so `tests/WebCRM.Data.Tests` leaves them out of the normal run, locally and in CI (its csproj passes
`--filter-not-trait "Category=Performance"` to the test runner).

Run only them with the `RunPerformance` property:

```
dotnet test --project tests/WebCRM.Data.Tests -p:RunPerformance=true
```

Today that is two tests:

- global search over 20,000 accounts and 20,000 contacts stays under the P7 budget once warm (needs SQL Server);
- the demo data generator produces the full default volume in under 60 seconds.

Add the trait to any new test whose pass or fail depends on a duration. Numbers from real measurements belong in
`docs/perf-baseline-demo-data.md`, not in assertions.

## Manual checks (browser)

Some behaviour only exists in a real browser, so bUnit cannot prove it. After changing the layout, `App.razor` or the
keyboard shortcuts, check these by hand:

- **Shortcuts with nothing focused.** Load any page (for example `/accounts`) and do not click anything, or click an empty
  area of the page. Then press `Ctrl+K` (the palette must open, not the browser's address-bar search), `/` (the search box
  gets the focus), `N` on a list page (the New form opens) and `E` on a record page (the form switches to edit mode).
  The listener is attached to `<body id="crm-body">`; keys with nothing focused are aimed at the body, so a listener on an
  element inside the layout would miss them.
- **Shortcuts while typing.** Type `e`, `n` and `/` in a text box: they must appear as text and trigger nothing.
  `Ctrl+K` and `Ctrl+S` still work there. `Ctrl+S` must not open the browser's "Save page as" dialog.
- **Ctrl+S saves the last edit.** In an edit form, change a field and press `Ctrl+S` without leaving the field: the saved
  value must be the new one.

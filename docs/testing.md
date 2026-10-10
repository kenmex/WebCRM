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

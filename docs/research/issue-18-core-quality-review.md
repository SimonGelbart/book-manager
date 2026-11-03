# Issue #18 — core quality review

Date: 2026-11-03. Reviewed implementation commit: `cbe9127`.
Baseline commit: `82d312f` (`main` before this branch's changes). Scope: the
implemented work-association/import-review slice and its managed test path.
Native Android, desktop-picker, and Windows runtime evidence remains in the
separate [capability-proof record](avalonia-proof-execution.md).

## Result

The checkpoint found one correctness defect and two test-structure gaps. The
defect was that `CatalogueApplication.SaveAsReadAsync` bypassed the mutation
gate used by import, clear, and ordinary reading-record writes. The fix routes
it through the same gate and adds a public-boundary regression for concurrent
import/save ordering. Cancellation now has a regression proving that the gate
is released after an interrupted import.

The architecture suite previously relied on manually assembled emitted-module
selections and did not prove that every source project had been selected. It
now checks all seven production projects, rejects unknown or mislabeled emitted
assemblies, and exercises the empty-selection guard. Android remains covered by
the project-reference graph; the plain `net10.0` architecture test host does
not load the Android target assembly.

The repeated gate implementations in `CatalogueApplication` were consolidated
into one helper. This makes the import, save, review-resolution, and clear
ordering invariant visible in one place. The focused fixes are in
[0fb16b5](https://github.com/SimonGelbart/book-manager/commit/0fb16b5),
[65dea30](https://github.com/SimonGelbart/book-manager/commit/65dea30), and
[ac70a2a](https://github.com/SimonGelbart/book-manager/commit/ac70a2a).

## Actual validation

Observed on Pop!_OS 24.04 with .NET SDK `10.0.114` and runtime `10.0.14`.
The resolved test stack is TUnit `1.77.0`, Microsoft.Testing.Platform
`2.5.2`, and Microsoft.Testing.Extensions.CodeCoverage `18.12.0`.

| Check | Command | Result |
|---|---|---|
| Locked restores | `dotnet restore tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --locked-mode`; same for `TomeRaider.Architecture.Tests` | Passed: 5 and 7 projects restored, 0 errors/warnings. |
| Release desktop build | `dotnet build src/TomeRaider.Desktop/TomeRaider.Desktop.csproj --configuration Release --no-restore` | Passed: 6 projects, 0 errors, 0 warnings. |
| Application suite | `dotnet test --project tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --configuration Release --no-restore --no-build --minimum-expected-tests 39` | Passed: 39/39, 0 failed, 0 skipped. |
| Architecture suite | `dotnet test --project tests/TomeRaider.Architecture.Tests/TomeRaider.Architecture.Tests.csproj --configuration Release --no-restore --no-build --minimum-expected-tests 10` | Passed: 10/10, 0 failed, 0 skipped. |
| Cobertura coverage | `dotnet test --project tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --configuration Release --no-restore --coverage --coverage-output application.cobertura.xml --coverage-output-format cobertura` | Passed; report generated under the test project's `TestResults` directory. |

The first architecture `--no-build` check used a stale Release binary and
correctly failed its minimum-test policy at 8 tests; rebuilding the changed
architecture project and rerunning produced the passing 10-test result above.

### Coverage baseline and final report

Coverage is recorded as line rate / branch rate. The baseline was collected at
`82d312f`; the final report was collected after the focused fixes. No numerical
threshold is proposed from this report alone.

| Module | Baseline | Final |
|---|---:|---:|
| Overall | 82.07% / 64.84% (1,748/2,130 lines; 402/620 branches) | 83.42% / 65.68% (1,474/1,767 lines; 400/609 branches) |
| Application | 99.23% / 86.11% | 99.00% / 86.11% |
| Infrastructure | 95.88% / 74.63% | 97.60% / 75.70% |
| Presentation | 43.69% / 39.89% | 42.34% / 40.23% |
| Domain | 0% / 0% | 0% / 0% |

The aggregate is not a quality claim. Application and Infrastructure have
strong managed-path exercise, while Domain has no direct behavior coverage and
Presentation coverage excludes Avalonia event-handler/XAML execution. The
review therefore added assertions and boundary/concurrency tests rather than a
percentage gate.

### Mutation baseline and final report

The local tool is pinned in
[.config/dotnet-tools.json](../../.config/dotnet-tools.json) as Stryker.NET
`5.0.0`. The committed configuration is
[stryker-config.json](../../tests/TomeRaider.Application.Tests/stryker-config.json);
it runs from `tests/TomeRaider.Application.Tests` with the MTP runner, Release
configuration, Basic mutation level, and a bounded scope containing
`WorkAssociationPolicy.cs` and `CatalogueApplication.cs`.

The initial association-policy campaign tested 15 mutants: 14 were killed and
one survived. The survivor changed `sourceMatches > 1` to
`sourceMatches >= 1`. The conflict-review test now asserts the exact reason,
and the rerun killed that mutation.

The final committed-scope run discovered 39 tests and created 54 mutants. Of
those, 18 were ignored by scope/block filtering; 36 were tested, 34 were
killed, 0 survived, and 2 timed out. Stryker reported a 100.00% mutation score.
Both timeouts were block-removal mutations of the centralized
`mutationGate.Release()` cleanup; removing either cleanup leaves queued work
blocked. They are deadlock signals for a required guard, not surviving
incorrect decisions. The MTP runner reported its documented preview warning,
so the result is useful evidence but is not yet a CI quality threshold.

## Findings by impact

### Resolved — correctness and data ordering

- `SaveAsReadAsync` bypassed the Application mutation gate, allowing a save to
  overlap an import or clear operation. The Application-boundary regression
  `SaveAsReadWaitsForAnActiveImportBeforeWritingTheRecord` now fails against
  the old implementation and passes with the fix.
- Cancellation was previously only covered for a null folder selection. The
  new `CancellationReleasesTheApplicationMutationGate` case cancels an active
  enumeration, verifies cancellation, and then proves a subsequent clear can
  acquire the gate.

### Resolved — architecture enforcement

- Project-reference checks now select and validate all production projects:
  Domain, Application, Infrastructure, Platform.Storage, Presentation, Desktop,
  and Android.
- Emitted-reference checks validate that the label and actual assembly agree; a
  deliberately mislabeled assembly is rejected. Forbidden references and empty
  selections remain explicit negative tests.
- The reviewed graph has the ADR 0003 direction: Domain is inward-only,
  Application owns adapter interfaces, Infrastructure and Platform.Storage sit
  outside it, Presentation depends on Application contracts, and hosts compose
  the outer implementations. No CQRS split was introduced.

### Remaining, nonblocking

- [SqliteCatalogue.cs](../../src/TomeRaider.Infrastructure/SqliteCatalogue.cs)
  remains a large adapter combining schema migration, SQL commands, review
  persistence, row mapping, and catalogue queries. It is coherent as the
  current SQLite seam, but should be split by responsibility before versions,
  reconciliation, and filtering make the file harder to navigate.
- [Work.cs](../../src/TomeRaider.Domain/Work.cs) has no direct behavior coverage
  and the current SQLite adapter stores reading fields directly in rows rather
  than constructing the domain entity. Add domain-level behavior tests and
  decide how the entity participates when personal-reading rules in issues #5
  and #6 grow.
- Presentation tests exercise `MainViewModel` ordering and captured intent, but
  not Avalonia control event handlers or native picker behavior. Add those tests
  only when a stable UI test seam exists; keep native evidence separate from
  managed tests.
- Broaden mutation scope to the SQLite association/persistence guards after the
  version and rescan features land. Do not add a numerical threshold until that
  larger baseline has been measured and reviewed.

All automated fixtures used here are synthetic and temporary. No private EPUB,
identifying metadata, or narrative was added to the report or repository.


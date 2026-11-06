# Issue #18 — core quality review

Date: 2026-11-03. Reviewed implementation commits through `63df474` (the
final code refactor before this report update).
Baseline commit: `82d312f` (`main` before this branch's changes). Scope: the
implemented work-association/import-review slice and its managed test path.
Native Android, desktop-picker, and Windows runtime evidence remains in the
separate [capability-proof record](avalonia-proof-execution.md).

## Result

The checkpoint found two correctness defects and two test-structure gaps. The
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
[ac70a2a](https://github.com/SimonGelbart/book-manager/commit/ac70a2a), and
[6ee626a](https://github.com/SimonGelbart/book-manager/commit/6ee626a).

The follow-up refactor was deliberately incremental. Application contracts are
split by concern in [0633da3](https://github.com/SimonGelbart/book-manager/commit/0633da3),
presentation operation serialization and selection ordering are in
[ad28c53](https://github.com/SimonGelbart/book-manager/commit/ad28c53), and the
SQLite adapter is split into focused schema, work-read, review, association,
and import-persistence modules across
[242a155](https://github.com/SimonGelbart/book-manager/commit/242a155),
[def40f8](https://github.com/SimonGelbart/book-manager/commit/def40f8),
[811e501](https://github.com/SimonGelbart/book-manager/commit/811e501),
[a8e189e](https://github.com/SimonGelbart/book-manager/commit/a8e189e),
[fc2cb9f](https://github.com/SimonGelbart/book-manager/commit/fc2cb9f), and
[63df474](https://github.com/SimonGelbart/book-manager/commit/63df474).
The public `IWorkCatalogue` interface remains cohesive by design; it was not
fragmented into shallow CQRS-style interfaces because the current Application
caller needs the whole catalogue and there is only one production adapter.

The final review also found that an unknown emitted-module label could be
detected and then indexed into the allowed-reference map, throwing before a
diagnostic was returned. The architecture check now evaluates dependency edges
only for recognized labels and reports the unknown selection explicitly.

## Actual validation

Observed on Pop!_OS 24.04 with .NET SDK `10.0.114` and runtime `10.0.14`.
The resolved test stack is TUnit `1.77.0`, Microsoft.Testing.Platform
`2.5.2`, and Microsoft.Testing.Extensions.CodeCoverage `18.12.0`.

| Check | Command | Result |
|---|---|---|
| Locked restores | `dotnet restore tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --locked-mode`; same for `TomeRaider.Architecture.Tests` | Passed: 5 and 7 projects restored, 0 errors/warnings. |
| Release desktop build | `dotnet build src/TomeRaider.Desktop/TomeRaider.Desktop.csproj --configuration Release --no-restore` | Passed: 6 projects, 0 errors, 0 warnings. |
| Application suite | `dotnet test --project tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --configuration Release --no-restore --minimum-expected-tests 40` | Passed: 40/40, 0 failed, 0 skipped. |
| Architecture suite | `dotnet test --project tests/TomeRaider.Architecture.Tests/TomeRaider.Architecture.Tests.csproj --configuration Release --no-restore --no-build --minimum-expected-tests 11` | Passed: 11/11, 0 failed, 0 skipped. |
| Cobertura coverage | `dotnet test --project tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --configuration Release --no-restore --coverage --coverage-output final-application.cobertura.xml --coverage-output-format cobertura` | Passed; report generated under the test project's `TestResults` directory. |

The first architecture `--no-build` check used a stale Release binary and
correctly failed its minimum-test policy at 8 tests. Rebuilding the changed
architecture project produced 10 passing tests; the final unknown-selection
regression brings the reviewed result to 11/11 above.

### Coverage baseline and final report

Coverage is recorded as line rate / branch rate. The baseline was collected at
`82d312f`; the final report was collected after the incremental refactors. No
numerical threshold is proposed from this report alone.

| Module | Baseline | Final |
|---|---:|---:|
| Overall | 82.07% / 64.84% (1,748/2,130 lines; 402/620 branches) | 84.16% / 66.23% (1,509/1,793 lines; 406/613 branches) |
| Application | 99.23% / 86.11% | 99.00% / 86.11% |
| Infrastructure | 95.88% / 74.63% | 97.74% / 75.70% |
| Presentation | 43.69% / 39.89% | 44.39% / 42.70% |
| Domain | 0% / 0% | 0% / 0% |

The aggregate is not a quality claim. Application and Infrastructure have
strong managed-path exercise, while Domain has no direct behavior coverage and
Presentation coverage excludes Avalonia event-handler/XAML execution. The
review therefore added assertions and boundary/concurrency tests rather than a
percentage gate.

Coverage exclusions/limits: the Microsoft coverage extension excludes the test
assembly by default; no source exclusion was configured. The Android emitted
assembly was not loaded by the plain `net10.0` architecture host, and generated
Avalonia/XAML/native platform execution was not part of this managed report.

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

The earlier final committed-scope run was measured at `63df474`, before the
review-fix commits below. It discovered 40 tests and created 54 mutants. Of
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

### Resolved — readability and locality

- Application records and ports are separated by concern rather than gathered
  in one contract file.
- `MainViewModel` has one serialization/error-handling seam, and selection is
  tested as waiting behind an active import.
- SQLite schema/migration, work reads, import persistence, import reviews, and
  association queries now have separate focused implementations. The original
  catalogue adapter file is 115 lines; the largest focused file is 357 lines.

### Remaining, nonblocking

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
- Keep `IWorkCatalogue` cohesive until a second adapter or an independent
  caller makes a smaller interface valuable; split it only when that seam has
  real leverage.

All automated fixtures used here are synthetic and temporary. No private EPUB,
identifying metadata, or narrative was added to the report or repository.

## Final completion-evidence snapshot — 2026-11-06

This append-only snapshot records the review-fix head and supersedes the older
counts where they differ. Production evidence below was measured at `351589e`
(`fix: preserve latest presentation selection intent`); the routed-event test
seam was then corrected in `8e81c94` without changing production behavior. The
documentation update follows both commits. The review-fix sequence is:

- `065a1b9`: one-way ListBox selection plus pending-selection protection, covering
  feedback-loop replay and work/note mixing while a selection is queued.
- `78a280f`: bounded waits and cleanup for blocked presentation concurrency
  fixtures.
- `351589e`: explicit VM-to-ListBox synchronization with a programmatic-event
  marker, dispatcher-safe updates, and an A→B→A control-event regression. This
  preserves the user's return to A after B was queued behind an import.
- `8e81c94`: raises the public Avalonia routed `SelectionChanged` event on the
  actual XAML `WorksList`, removing private-handler reflection from the test.

The final managed validation was:

| Check | Command | Result at `351589e` / `8e81c94` |
|---|---|---|
| Application build | `dotnet build tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --configuration Release --no-restore -v minimal` | Passed: 5 projects, 0 errors, 0 warnings. |
| Application suite | `dotnet test --project tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --configuration Release --no-build --no-restore` | Passed: 43/43, 0 failed, 0 skipped. |
| Architecture build | `dotnet build tests/TomeRaider.Architecture.Tests/TomeRaider.Architecture.Tests.csproj --configuration Release --no-restore -v minimal` | Passed: 7 projects, 0 errors, 0 warnings. |
| Architecture suite | `dotnet test --project tests/TomeRaider.Architecture.Tests/TomeRaider.Architecture.Tests.csproj --configuration Release --no-build --no-restore` | Passed: 11/11, 0 failed, 0 skipped. |
| Release desktop build | `dotnet build src/TomeRaider.Desktop/TomeRaider.Desktop.csproj --configuration Release --no-restore -v minimal` | Passed: 6 projects, 0 errors, 0 warnings. |
| Cobertura coverage | `dotnet test --project tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --configuration Release --no-build --no-restore --coverage --coverage-output review-followup-event.cobertura.xml --coverage-output-format cobertura` | Passed: 43/43; overall 88.52% line / 69.39% branch; Application 99.00% / 86.11%; Infrastructure 97.74% / 75.70%; Presentation 65.13% / 55.45%; Domain 0% / 0%. |

The coverage report was generated from the Release binaries after the final
code changes. It excludes the test assembly and does not claim Avalonia native
or headless UI execution. The earlier `84.16% / 66.23%` aggregate remains the
measurement from the pre-review-fix snapshot, not a result of this head.

The bounded Stryker campaign was rerun from
`tests/TomeRaider.Application.Tests` at `351589e` with
`dotnet tool run dotnet-stryker --config-file stryker-config.json`. It found 43
tests and created 54 mutants: 18 ignored by scope/block filtering, 36 tested,
34 killed, 0 survived, 1 timeout, and 1 test-host runtime error. Stryker
reported a 100.00% score and an elapsed runtime of 33.79 seconds. The timeout
is a mutation of required mutation-gate cleanup; the runtime error was an MTP
test-host crash. Because the MTP runner is still in preview and this run had a
runtime error, this remains diagnostic evidence rather than a CI quality gate.

The follow-up `8e81c94` changes only how the presentation regression raises the
already-covered selection event; it does not change the Application mutation
scope or production Application code.

The old 54-mutant result in the earlier section is therefore explicitly tied
to `63df474`; it was not silently presented as a rerun after `065a1b9` or
`78a280f`. The remaining limitations are unchanged: no native/headless
Avalonia session, no Android runtime execution, and no direct Domain behavior
coverage. The new regression exercises the real XAML `MainView` and its
public routed selection event through the managed test seam; it does not replace
native device or desktop-picker evidence.

## Dispatcher follow-up snapshot — 2026-11-06

The P2 review finding on `573970b` was confirmed by the cited push-CI failure
and by an independent local Release failure: the ViewModel details signal did
not prove that the posted `ListBox` synchronization had run. Commit `5d07375`
(`fix: run presentation proofs on controlled dispatcher`) addresses that
finding within the existing Presentation capability-proof scope:

- `MainView.SyncSelectedWork` posts through `WorksList.Dispatcher`, keeping the
  control's dispatcher as the synchronization target.
- Both affected `MainView` regressions create the real XAML control and execute
  setup, routed events, queued selection work, and final assertions inside a
  dedicated Avalonia dispatcher lifecycle.
- The tests assert the actual `WorksList.SelectedItem` as well as the
  ViewModel/details state, retain the five-second deadlines, and wait for the
  dispatcher thread to exit before fixture cleanup.

Validation at `5d07375`:

| Check | Result |
|---|---|
| Application Release build | Passed: 5 projects, 0 errors, 0 warnings. |
| Application Release suite | Passed; repeated runs completed successfully. |
| Architecture Release build and suite | Passed; 0 errors and 0 warnings; 11 tests. |
| Release desktop build | Passed: 6 projects, 0 errors, 0 warnings. |
| Cobertura coverage | Passed; 88.25% line / 69.23% branch overall in the generated report. |

The dispatcher follow-up does not claim native/headless Avalonia, Android
runtime, or Domain behavior coverage, and Stryker was not rerun for this
test-boundary correction. The earlier mutation result and its limitations
remain tied to the revisions recorded above.

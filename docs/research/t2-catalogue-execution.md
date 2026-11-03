# T2 catalogue execution record

Date: 2026-11-03. Scope: issue [#3 — Durable folder catalogue and metadata](https://github.com/SimonGelbart/book-manager/issues/3), implementing T2 from the [approved implementation plan](../planning/implementation-plan.md).

## Implemented slice

- The Application boundary now imports folders, exposes durable collection summaries and work details, and reports incomplete scans without treating them as confirmed absence.
- SQLite stores source-independent Works, DownloadedVersions, EPUB copies, categorized tags, imported descriptions and author notes, while personal reading information remains on the Work.
- EPUB extraction reads all spine documents, including split AO3 prefaces, source links, categorized tag anchors, reported word/chapter statistics, summaries, and author notes.
- Avalonia presentation displays the collection and selected work details through the Application boundary, with readable HTML descriptions, collapsed categorized-tag groups, explicit scope/status labels, and a confirmation-protected action to clear the durable collection.
- Clearing the collection removes saved works, versions, copies, tags, and personal reading records in one SQLite transaction while preserving the remembered folder.
- T2 keeps separate files with the same AO3 evidence on separate internal Works until the T3 association/review policy is implemented.

## Executed validation

| Check | Command | Result |
|---|---|---|
| Locked restore | `dotnet restore TomeRaider.slnx --locked-mode` | Passed: 10 projects, 0 errors, 0 warnings. |
| Desktop Release build | `dotnet build src/TomeRaider.Desktop/TomeRaider.Desktop.csproj --configuration Release --no-restore` | Passed: 6 projects, 0 errors, 0 warnings. |
| Application behavior/integration tests | `dotnet run --project tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --configuration Release` | Passed: 22/22, 0 failed, 0 skipped. Includes real temporary SQLite reopen, generic/AO3 import, split-preface metadata, separate internal works for same-AO3 evidence, unchanged rescan, incomplete scan, readable HTML details, grouped tags, clear-collection persistence, and view-model reset behavior. |
| Architecture tests | `dotnet run --project tests/TomeRaider.Architecture.Tests/TomeRaider.Architecture.Tests.csproj --configuration Release` | Passed: 8/8, 0 failed, 0 skipped. Includes forbidden dependency and empty-selection failures. |
| Android Release publish/install | `dotnet publish src/TomeRaider.Android/TomeRaider.Android.csproj --configuration Release --no-restore -p:AndroidSdkDirectory=/home/simon/.local/share/tome-raider/android-sdk -p:JavaSdkDirectory=/home/simon/.local/share/tome-raider/jdk -p:RunAOTCompilation=false` | Passed; signed APK installed on `emulator-5554`, launched successfully, and matched the local SHA-256 `49653bd3233567374da6fe88e6f2329cf41196fd42bd7077a3ced4cceafd3aed`. |
| Diff whitespace | `git diff --check main...HEAD` | Passed with no findings. |

All EPUB fixtures used by these tests are synthetic and created in temporary directories. No private EPUB, identifying metadata, or narrative is committed.

## Evidence limits

This record proves the managed Application/SQLite path, Release desktop build, and Android emulator install/launch. It does not claim interactive native folder-picker evidence, Android document-grant/restart evidence, or Windows runtime evidence; those remain separate target-platform checks as described in the testing conventions.

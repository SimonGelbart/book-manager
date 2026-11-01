# Avalonia/.NET capability proof: execution record

Date: 2026-11-01. Direction: [ADR 0001](../adr/0001-avalonia-and-shared-dotnet-core.md). Scope: the [first capability proof](../architecture/cross-platform-proposal.md#first-capability-proof).

## Current result

The selected .NET/Avalonia route starts and builds in the owner's local Pop!_OS environment. The shared import and persistence workflow is implemented and passes its executable TUnit and architecture checks. Desktop startup and cross-target compilation are verified; native folder picking, Android execution, retained grants, and Windows runtime behavior remain explicitly unverified.

The earlier CoreCLR startup failure remains an observation from a separate agent workspace. It was not reproduced here: the system SDK starts normally.

## Local toolchain

Observed on Pop!_OS 24.04 (`pop`, x86_64), kernel `7.2.6-76070206-generic`:

| Component | Observed value |
|---|---|
| .NET SDK | 10.0.114 (the owner-requested local SDK; it starts successfully) |
| .NET host/runtime | 10.0.14; Microsoft.NETCore.App and Microsoft.AspNetCore.App |
| Workload set | 10.0.402 |
| Android workload | manifest `36.1.118/10.0.100` |
| Android API/build tools/platform tools | API 36, build-tools 36.0.0, platform-tools 36.0.0 |
| Java used for Android build | Microsoft OpenJDK 17.0.14, installed under `/home/simon/.local/share/tome-raider/jdk` |
| Avalonia packages | 12.1.4 |
| SQLite provider/bundle | Microsoft.Data.Sqlite 10.0.14; SQLitePCLRaw.bundle_e_sqlite3 2.1.14 |
| Test runner | TUnit 1.77.0; Microsoft.Testing.Platform 2.5.2 resolved transitively |

The repository pins the runnable local SDK in `global.json`. The Android SDK and JDK are local development prerequisites and are not repository assets.

Resolved NuGet graphs are captured in the per-project `packages.lock.json` files and CI restores the test graphs in locked mode.

The earlier candidate research names SDK 10.0.403 and Microsoft OpenJDK 21 as the preferred fresh pins. This execution used the SDK already installed in the requested local environment (10.0.114) and the JDK selected by the Android dependency installer (17.0.14). The successful build is evidence for this exact local combination; it is not a claim that the 10.0.403/JDK 21 combination has also been validated.

The Android workload was installed with:

```sh
dotnet workload install android --version 10.0.402
```

The dependency installer supplied the local Android SDK/JDK used for the build. `adb devices -l` ran successfully but listed no devices.

## Implemented proof slice

- `TomeRaider.Application` exposes one ordinary Application seam for folder selection/restoration, recursive EPUB enumeration, stream-based extraction, import, and reading-record changes.
- `TomeRaider.Infrastructure` provides base EPUB metadata extraction, optional AO3 URL/ID enrichment, a physical-folder test adapter, and a file-backed SQLite catalogue.
- Generic EPUBs without an AO3 link or ID receive a generated internal `Guid` work identity. AO3 metadata is enrichment, not the identity.
- The shared Avalonia UI is referenced by separate Desktop and Android entry projects.
- `AvaloniaStorageFileSource` keeps Avalonia storage types outside the Application/Domain layers, uses storage streams rather than raw paths, and saves/restores a folder bookmark through a JSON store.
- The synthetic test EPUBs are generated in temporary test directories; no private EPUB or identifying metadata is committed.

## Validation matrix

| Check | Actual command/evidence | Result |
|---|---|---|
| SDK starts locally | `dotnet --info`, `dotnet --list-sdks`, `dotnet --list-runtimes` | Passed with SDK 10.0.114 and runtime 10.0.14. |
| Shared generic/AO3 import and SQLite reopen | `dotnet test --project tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --no-build` | Passed: 2/2 tests. Both cases use the same Application and SQLite implementation; the status and note were recovered after constructing a new catalogue instance. |
| Architecture direction | `dotnet test --project tests/TomeRaider.Architecture.Tests/TomeRaider.Architecture.Tests.csproj --no-build` | Passed: 5/5 tests. Project references, cycle absence, inner-layer restrictions, emitted production references, forbidden-edge rejection, and empty-selection rejection are checked. |
| Full repository test suite | `dotnet test --solution TomeRaider.slnx -p:AndroidSdkDirectory=... -p:JavaSdkDirectory=...` | Passed: 7/7 tests, 0 failed, 0 skipped. |
| Desktop build | `dotnet build src/TomeRaider.Desktop/TomeRaider.Desktop.csproj --no-restore` | Passed: 6 projects, 0 errors, 0 warnings. |
| Pop!_OS display startup | `XDG_SESSION_TYPE=wayland`, `WAYLAND_DISPLAY=wayland-1`, `DISPLAY=:1`, `XDG_CURRENT_DESKTOP=COSMIC`; `dotnet run --project src/TomeRaider.Desktop/TomeRaider.Desktop.csproj --no-build` | Passed native startup for an 8-second run with no Avalonia or CoreCLR exception. The timeout exit 124 was the intentional process stop. The actual folder picker and UI import were not exercised in this automated session. |
| Desktop workflow seam | Application test using `PhysicalFileSource` over a synthetic folder with recursive enumeration | Passed through the public Application interface; this is not a substitute for native picker evidence. |
| Private representative EPUB | No private sample path was supplied in this workspace | Not run; only synthetic fixtures were used and committed. |
| Android compile | `dotnet build src/TomeRaider.Android/TomeRaider.Android.csproj --no-restore -p:AndroidSdkDirectory=/home/simon/.local/share/tome-raider/android-sdk -p:JavaSdkDirectory=/home/simon/.local/share/tome-raider/jdk` | Passed: 6 projects, 0 errors, 0 warnings. |
| Android package | `dotnet publish src/TomeRaider.Android/TomeRaider.Android.csproj -c Release --no-restore` with the same SDK/JDK paths | Passed with `RunAOTCompilation=false`; signed APK written under `src/TomeRaider.Android/bin/Release/net10.0-android36.0/publish/`. A default Release AOT attempt failed in the cross-compiler at `System.GC:CallFinalize` (`Method has zero rva`), so the proof APK deliberately keeps AOT disabled until it is validated on-device. |
| Android picker/read/restart/device restart | `adb devices -l` | Not run: no Android device or emulator is connected. Bookmark restoration, document-tree enumeration, EPUB stream reading, SQLite native loading, and app/device restart behavior remain open evidence. |
| Windows build/package | `dotnet publish src/TomeRaider.Desktop/TomeRaider.Desktop.csproj -c Release -r win-x64 --self-contained false` | Passed. Windows x64 publish output was produced; Windows runtime behavior was not executed. |

## Remaining gate

The implementation closes the local SDK startup issue and proves the shared managed path, but it does not claim the full target-platform gate is closed. Before treating this proof as complete, run the Android APK on Android 16 with an allowed document-tree grant, verify enumeration and reading after app and device restart, and exercise the Pop!_OS picker/import flow interactively. Keep Windows runtime and packaging claims limited to the published build until a Windows run is available.

Full collection import, duplicate/version rules, filtering, ratings, synchronization, and a reader remain outside this proof.

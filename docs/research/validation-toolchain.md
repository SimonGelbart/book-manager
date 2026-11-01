# Toolchain candidates for the Avalonia validation probe

Checked on **2026-10-31** against official release metadata, documentation, and published NuGet artifacts. Avalonia/.NET is the user's selected route for the technical proof. These findings establish candidate dependencies; they do **not** establish successful builds, graphics startup, Android file access, or a production-ready stack.

## Freshness preference and recommendation

The user can install another SDK and wants the most up-to-date versions possible. Recommend the newest stable releases compatible with the desktop and Android projects, with exact pins for reproducible builds and deliberate updates as releases become available.

Rechecked on 2026-10-31: **.NET SDK 10.0.403** is already the latest stable SDK, and **Avalonia 12.1.4** is the latest stable Avalonia package. **.NET 11** is available as a release candidate, `11.0.100-rc.3.26522.148`; it is not yet the stable baseline. Keep the .NET 10 candidate for the proof, then assess .NET 11 after its stable release and Android/Avalonia compatibility checks. No downgrade is proposed to work around the agent workspace's startup failure. [D7], [A11]

Latest compatible does not mean every dependency's highest major version: Android workloads, JDK, and native SQLite assets must form a supported, working combination. Record any intentional version lag and its reason, and reassess it when updating the toolchain.

## Candidate pins

| Component | Candidate pin | Verified basis |
|---|---|---|
| .NET SDK | **10.0.403** | Microsoft's .NET 10 release feed lists this stable SDK, containing runtime **10.0.14**, released 2026-10-25. [D1] |
| Avalonia packages | **12.1.4** for `Avalonia`, `Avalonia.Desktop`, `Avalonia.Android`, and `Avalonia.Themes.Fluent` | Published stable packages. Desktop includes an actual `net10.0` dependency group; Android includes actual `net10.0-android36.0` assets, rather than only NuGet's computed compatibility. [A1], [A2], [A3], [A4] |
| Avalonia templates | **12.1.4** | Install with `dotnet new install Avalonia.Templates@12.1.4`. Its published cross-platform template contains separate desktop and Android entry projects referencing shared C# UI; the Android template additionally references `Xamarin.AndroidX.Core.SplashScreen` **1.2.0.2**. [A5], [A6] |
| Android workload | Workload set **10.0.402**, Android component **36.1.118** | The official servicing release documents `dotnet workload install android --version 10.0.402` and reports manifest `36.1.118/10.0.100`. The SDK, workload set, and Android component have different version numbers. [D2], [D3] |
| Java toolchain | Microsoft OpenJDK **21**; current listed patch **21.0.13.0.2** | The Android dependency guide recommends JDK 21; the download page provides this patch for Linux and Windows. A newer JDK major is not an equivalent pin. [D4], [D5] |
| Embedded SQLite | `Microsoft.Data.Sqlite` **10.0.14** plus an explicit `SQLitePCLRaw.bundle_e_sqlite3` **2.1.14** | The Microsoft package depends on its Core package >=10.0.14 and SQLitePCLRaw bundle/core >=2.1.13. The proposed bundle stays within that 2.1 line; it is not the newest major of SQLitePCLRaw. [S1], [S2], [S3] |

The official SDK feed supplies per-platform download URLs and SHA-512 hashes. For Linux x64, its SDK archive is `https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-linux-x64.tar.gz`, with SHA-512:

```text
b7b3d8461cf98b07d2552ce9199702fa3ad010ac2671a5f8cd5136f7534e50f6855ba7bbb02b11494563f1d04156276a31cda1103b900e5eecd0d9473dc7b9c5
```

Windows x64 ZIP/installer records are in the same feed. Pin the SDK selection and capture the installed workload version separately; a package or template pin does not pin the Android workload. [D1], [D3]

## Target and dependency checks

**Proposed probe structure:** a target-neutral `net10.0` C# import/query/storage core, shared Avalonia UI, a `net10.0` desktop entry project, and a `net10.0-android36.0` Android entry project. Both entry projects reference the same core in-process. This follows Avalonia's documented shared-project structure and the included package targets; it does not depend on a desktop .NET/Python sidecar becoming available on Android. [A2], [A3], [A7]

The Android package brings AndroidX AppCompat >=1.7.1.3, DocumentFile >=1.1.0.3, and Window >=1.5.1.2. Avalonia's graphics packages depend on SkiaSharp >=3.119.4 and HarfBuzzSharp >=8.3.1.3, including Linux native asset packages. These are published dependency minima, not the final resolved graph or proof that every native library loads. [A3], [A8], [A9]

For API 36, the Android 36.1.118 source names build-tools **36.0.0** and platform-tools **36.0.0**. Microsoft's `InstallAndroidDependencies` MSBuild target inspects the actual Android project and installs its required SDK/JDK components; use that result rather than the guide's older API-34 example. SDK/JDK paths must be absolute. Record the installed components and `java -version`. The `android` workload suffices for Avalonia's .NET Android target; this does not require a MAUI UI project. [D2], [D4], [D6]

Linux still needs Avalonia's documented native system libraries and a usable display backend; Windows uses its own platform assets. Compilation alone does not validate the user's Pop!_OS/COSMIC session. See the [runtime research](runtime-fit-existing-skills.md) for the display and storage constraints. [A10]

## SQLite artifact evidence

Microsoft documents that the complete `Microsoft.Data.Sqlite` package brings the native bundle and initializes it; `Microsoft.Data.Sqlite.Core` alone is not that bundle. This permits the same managed database code with platform-specific native SQLite assets. [S3]

The following published **2.1.14 NuGet ZIPs were read in memory**, including their `.nuspec` and nested Android archive:

- The bundle has a real `net6.0-android31.0` dependency group selecting `SQLitePCLRaw.lib.e_sqlite3.android` and its provider. Its generic .NET Standard group instead selects the desktop native package. Compatibility with the proposed .NET 10 heads must still be confirmed by restore. [S4]
- The desktop package contains `runtimes/linux-x64/native/libe_sqlite3.so` and Windows x64/ARM64/x86 DLLs, among other architectures. [S5]
- The Android package contains an AAR with `jni/arm64-v8a`, `jni/x86_64`, `jni/armeabi-v7a`, and `jni/x86` SQLite libraries. All inspected ELF load segments report 16,384-byte alignment. That inspection does not establish final APK ZIP alignment, native loading, or Android 16 device behavior. [S6]

## Evidence still required

This research performed no application build or device test. Record SDK/workload versions, resolved packages, and build results in the probe's validation log. Confirm Linux and Windows outputs separately. For Android, inspect the resolved platform dependencies and APK native ABIs, then run the same core through a real Android head. On each executed platform, create/write/close/reopen/query a local database and exercise EPUB streams. Folder grants, restart behavior, graphics, and packaging remain runtime checks. A successful Linux build cannot close those Windows/Android checks.

[D1]: https://builds.dotnet.microsoft.com/dotnet/release-metadata/10.0/releases.json "Official .NET 10 release and SDK download metadata"
[D2]: https://github.com/dotnet/android/releases/tag/36.1.118 "Official .NET Android servicing release and workload-set command"
[D3]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-workload-install "Workload-set selection and SDK context"
[D4]: https://learn.microsoft.com/en-us/dotnet/android/getting-started/installation/dependencies "Android SDK/JDK requirements and dependency installation target"
[D5]: https://learn.microsoft.com/en-us/java/openjdk/download "Microsoft OpenJDK versions and platform downloads"
[D6]: https://github.com/dotnet/android/blob/36.1.118/Configuration.props "Pinned Android source: API, build-tools and platform-tools versions"
[D7]: https://builds.dotnet.microsoft.com/dotnet/release-metadata/releases-index.json "Current stable and prerelease SDK channels"
[A1]: https://www.nuget.org/packages/Avalonia/12.1.4 "Avalonia stable package and included frameworks"
[A2]: https://www.nuget.org/packages/Avalonia.Desktop/12.1.4 "Desktop target and dependencies"
[A3]: https://www.nuget.org/packages/Avalonia.Android/12.1.4 "Android 36 target and AndroidX dependencies"
[A4]: https://www.nuget.org/packages/Avalonia.Themes.Fluent/12.1.4 "Matching Fluent theme package"
[A5]: https://www.nuget.org/packages/Avalonia.Templates/12.1.4 "Pinned template installation"
[A6]: https://api.nuget.org/v3-flatcontainer/avalonia.templates/12.1.4/avalonia.templates.12.1.4.nupkg "Published template artifact: content/templates/csharp/xplat project files"
[A7]: https://docs.avaloniaui.net/docs/fundamentals/cross-platform-architecture "Shared C# UI and business logic with platform entry projects"
[A8]: https://www.nuget.org/packages/Avalonia.Skia/12.1.4 "SkiaSharp and HarfBuzzSharp dependency groups"
[A9]: https://www.nuget.org/packages/Avalonia.HarfBuzz/12.1.4 "HarfBuzz native dependency groups"
[A10]: https://docs.avaloniaui.net/docs/platform-specific-guides/linux "Linux native libraries and display backends"
[A11]: https://www.nuget.org/packages/Avalonia "Latest stable Avalonia package"
[S1]: https://www.nuget.org/packages/Microsoft.Data.Sqlite/10.0.14 "Microsoft managed SQLite provider and dependencies"
[S2]: https://www.nuget.org/packages/SQLitePCLRaw.bundle_e_sqlite3/2.1.14 "Selected SQLitePCLRaw maintenance version"
[S3]: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions "Microsoft's native bundle and initialization documentation"
[S4]: https://api.nuget.org/v3-flatcontainer/sqlitepclraw.bundle_e_sqlite3/2.1.14/sqlitepclraw.bundle_e_sqlite3.2.1.14.nupkg "Bundle nuspec: Android and generic native dependency groups"
[S5]: https://api.nuget.org/v3-flatcontainer/sqlitepclraw.lib.e_sqlite3/2.1.14/sqlitepclraw.lib.e_sqlite3.2.1.14.nupkg "Desktop runtime-specific native assets"
[S6]: https://api.nuget.org/v3-flatcontainer/sqlitepclraw.lib.e_sqlite3.android/2.1.14/sqlitepclraw.lib.e_sqlite3.android.2.1.14.nupkg "Android native AAR assets and inspected ELF load segments"

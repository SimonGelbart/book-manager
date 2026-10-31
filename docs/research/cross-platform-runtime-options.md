# Cross-platform runtime options

Research checked on 2026-10-31. Scope: a Linux MVP, a preferred Windows release, and a future Android manager/reader for the personal AO3 collection. Files and the database should work locally; synchronization is later work. **No runtime has been selected.** Documentation and upstream source support the claims below; no application or device prototype was run.

## Documented capabilities

| Concern | Tauri 2 | Flutter |
|---|---|---|
| Linux, Windows, Android | All are supported application targets from one codebase. [T1] | All are supported deployment targets, subject to the listed OS/architecture versions. [F1] |
| Shared application code | Web frontend plus Rust application logic, with native integrations where necessary. [T1] | Dart application/UI code, with platform channels or native FFI integrations where necessary. [F2] |
| Local SQL | The official SQL plugin lists all three targets and includes an optional SQLite driver. [T3] | The official SQLite recipe uses `sqflite`, which itself supports Android/iOS/macOS; its maintainers provide `sqflite_common_ffi` for Linux/Windows and also advertise Android support. This is a package choice, not automatic framework coverage. [F4], [F5], [F6] |
| Desktop folder selection | The official dialog plugin supports folder picking on Linux and Windows. [T4] | The Flutter-published `file_selector` plugin lists directory selection on Linux and Windows. [F3] |
| Android folder selection | The official dialog plugin explicitly lacks an Android folder picker. Its file picker returns content URIs, which the filesystem plugin can handle. [T4] | `file_selector` lists Android directory selection, but the inspected implementation has restrictions described below. [F3], [F7], [F8] |

**Inference for this project:** either runtime can host a shared domain model, metadata parser, import decisions, queries, and UI across the three targets. The shareable importer should consume bytes or streams obtained through a platform adapter; a desktop pathname is not a portable Android input. Actual parser/database dependencies still need all-target verification. SQLite is embedded and does not require a hosted database server. Neither framework supplies collection synchronization merely by sharing the application code. [T1], [F2], [S1]

## Android storage is a separate integration

Android's Storage Access Framework returns document/tree URIs and user-granted access. A directory-tree grant covers the selected directory and descendants; lasting access across restarts requires taking the offered persistable URI permission. Moved/deleted documents can invalidate access. Android 11+ prevents selecting storage roots and the top-level Download directory as tree grants. Android also cautions that enumerating large trees can be slow. These rules apply regardless of the UI framework. [A1]

- **Tauri:** default filesystem access is restricted to application storage; plugin capabilities and OS grants are separate requirements. The official dialog plugin's Android file support is useful but does not implement the required persistent folder-selection workflow. A native mobile plugin can bridge Android APIs; such a bridge or another validated plugin would be additional work. [T4], [T5], [T6]
- **Flutter:** the advertised folder picker is not evidence of unrestricted filesystem access. At inspected upstream commit `4cb0506dddae4f9861145c48f4b27b17907fe251`, `getDirectoryPath()` invokes the tree picker and converts the result to a raw path. Its helper accepts only primary on-device external-storage documents and rejects non-primary volumes and other provider authorities. Individual-file selection copies content into cache. This upstream snapshot may differ from the package release ultimately pinned. [F7], [F8]

**Inference:** retain an explicit Android URI/grant adapter, and validate provider access, persistence after restart, and rescanning. Flutter's existing folder picker gives a different starting point from Tauri's missing picker, but does not remove this validation. App-owned cached copies also differ from linking to originals in a user-managed folder. [A1], [T4], [F7], [F8]

## Build and packaging prerequisites

| Target | Tauri 2 | Flutter |
|---|---|---|
| Linux | Rust and distribution-specific native development libraries, including WebKitGTK; a JavaScript frontend also needs its frontend toolchain. Linux packaging is target-specific. [T2], [T7] | Flutter SDK plus Clang, CMake, Ninja, pkg-config, GTK development libraries and related dependencies. Distribute the application bundle with its assets/native libraries and check system-library requirements. [F9], [F10] |
| Windows | Normal Windows development needs Rust, MSVC C++ Build Tools and WebView2. MSI/NSIS packaging is documented. Linux/macOS cross-compilation exists for NSIS with caveats; Tauri recommends Windows/VM/CI builds ahead of that route. [T2], [T8] | The documented setup uses Windows and Visual Studio's Desktop development with C++ workload. Distribution must include the executable and required DLL/assets; an installer is an additional packaging choice. [F11], [F12] |
| Android | Android Studio/JDK, SDK/build tools, NDK and Android Rust targets; native project setup plus APK/AAB and signing steps. [T2], [T9] | Flutter SDK, Android Studio/SDK tooling, accepted SDK licenses and device/emulator setup; separate APK/AAB release and signing steps. [F13], [F14] |

Sharing source therefore does not produce one universal binary or remove platform build/testing work. A Windows build runner is the straightforward Windows path for either option; Android can be developed from Linux. Local installation is compatible with the no-hosted-server requirement. [T2], [T8], [T9], [F11], [F13], [F14]

## Implications and remaining evidence

These are engineering implications, **not a stack decision**:

- Tauri preserves a web-UI approach alongside a reusable Rust core; Flutter preserves a Dart UI/application approach. Responsive phone layouts and native file interactions remain deliberate work in either case. [T1], [F2]
- Keep AO3 work identity, downloaded-version comparison, reading status, notes, and query behavior above the storage/platform adapters. Keep location/permission handling below them. This is a proposed separation, not code already demonstrated.
- Before selecting, prove one real-folder import on Linux, local database reopen, and the future Android tree/URI path on a real device. Include restart, lost grant, large-folder progress, and selected-provider cases. No throughput, memory, EPUB-reader integration, or synchronization mechanism has been established by this research.
- Audit each chosen plugin's target matrix. Platform support by the framework does not imply equivalent support by every picker, database, or future reader dependency. [T4], [F3], [F4], [F5], [F6]

For comparison only: Electron officially targets Windows/macOS/Linux desktop, so it is not itself the Android runtime. Capacitor's official core targets are Android, iOS and web; adding a separate desktop runtime would introduce another integration boundary. Neither is evaluated further here. [E1], [C1]

## Browser-only deployment

A web UI can be shared and packaged in a native application, or deployed directly in a browser. For the browser-only option, MDN marks `showDirectoryPicker()` as limited availability and requiring a secure context. Uniform folder access across browsers therefore needs verification. [W1]

**Inference for this project:** browser-only deployment remains possible, but should not be chosen on the assumption that existing-folder cataloguing works identically everywhere. Compare its actual import and permission workflow with the native adapters before selecting it.

[T1]: https://v2.tauri.app/ "Tauri 2 platform and language overview"
[T2]: https://v2.tauri.app/start/prerequisites/ "Tauri platform build prerequisites"
[T3]: https://v2.tauri.app/plugin/sql/ "Tauri official SQL plugin"
[T4]: https://v2.tauri.app/plugin/dialog/ "Tauri dialog platform support and returned URI formats"
[T5]: https://v2.tauri.app/plugin/file-system/ "Tauri filesystem access, configuration and scopes"
[T6]: https://v2.tauri.app/develop/plugins/develop-mobile/ "Tauri native mobile plugin development"
[T7]: https://v2.tauri.app/distribute/ "Tauri distribution targets"
[T8]: https://v2.tauri.app/distribute/windows-installer/ "Tauri Windows installers and cross-compilation caveats"
[T9]: https://v2.tauri.app/distribute/google-play/ "Tauri Android packaging"
[F1]: https://docs.flutter.dev/reference/supported-platforms "Flutter supported deployment platforms"
[F2]: https://docs.flutter.dev/resources/architectural-overview "Flutter application architecture and platform integration"
[F3]: https://pub.dev/packages/file_selector "Flutter-published file_selector feature matrix"
[F4]: https://docs.flutter.dev/cookbook/persistence/sqlite "Flutter SQLite recipe and platform caveat"
[F5]: https://pub.dev/packages/sqflite "SQLite package maintainer's platform support"
[F6]: https://pub.dev/packages/sqflite_common_ffi "SQLite FFI implementation maintainer's platform support"
[F7]: https://github.com/flutter/packages/blob/4cb0506dddae4f9861145c48f4b27b17907fe251/packages/file_selector/file_selector_android/android/src/main/java/dev/flutter/packages/file_selector_android/FileSelectorApiImpl.java#L203-L245 "Inspected Android directory picker implementation"
[F8]: https://github.com/flutter/packages/blob/4cb0506dddae4f9861145c48f4b27b17907fe251/packages/file_selector/file_selector_android/android/src/main/java/dev/flutter/packages/file_selector_android/FileUtils.java#L50-L154 "Inspected directory URI restrictions and cache-copy helper"
[F9]: https://docs.flutter.dev/platform-integration/linux/setup "Flutter Linux build prerequisites"
[F10]: https://docs.flutter.dev/platform-integration/linux/building "Flutter Linux distribution bundle"
[F11]: https://docs.flutter.dev/platform-integration/windows/setup "Flutter Windows build prerequisites"
[F12]: https://docs.flutter.dev/platform-integration/windows/building "Flutter Windows distribution"
[F13]: https://docs.flutter.dev/platform-integration/android/setup "Flutter Android prerequisites and development hosts"
[F14]: https://docs.flutter.dev/deployment/android "Flutter Android release packages"
[A1]: https://developer.android.com/training/data-storage/shared/documents-files "Android Storage Access Framework, restrictions and persisted URI grants"
[S1]: https://www.sqlite.org/serverless.html "SQLite embedded serverless operation"
[E1]: https://www.electronjs.org/docs/latest/ "Electron officially supported desktop targets"
[C1]: https://capacitorjs.com/docs "Capacitor official native/mobile/web targets"
[W1]: https://developer.mozilla.org/en-US/docs/Web/API/Window/showDirectoryPicker "Browser directory-picker availability and secure-context requirement"

# Runtime fit with existing skills

Checked on 2026-10-31. The user knows .NET/C#, Python, and React, and is willing to learn Kotlin, Rust, or Go where useful. The target remains Linux first, Windows preferred, and a future Android manager/reader, with a shared local core and no required hosted server. **No stack decision or device/plugin test is recorded here.**

## Verified framework capabilities

| Concern | Tauri 2 with React and Rust | Avalonia with .NET/C# |
|---|---|---|
| Required targets | Officially targets Linux, Windows, and Android from one codebase. [T1] | Officially targets Linux, Windows, and Android; support tiers depend on specific OS versions. [A1] |
| Shared core and UI | JavaScript frontend and Rust application logic can be shared, with native mobile integrations when needed. [T1], [T3] | Official cross-platform structure shares views, view models, and business logic in a core project, referenced by desktop and .NET Android entry projects. [A2] |
| Android folders | Official dialog plugin supports files/content URIs but explicitly lacks an Android folder picker. A Kotlin/Java plugin can bridge additional native APIs. [T2], [T3] | StorageProvider exposes file/folder pickers, storage-item streams, folder enumeration, and bookmarks on Android as well as desktop. [A3], [A4], [A5] |
| Local persistence | Official SQL plugin lists all three targets and offers SQLite. [T4] | Microsoft.Data.Sqlite provides an ADO.NET SQLite API; its native SQLite bundle and target compatibility are dependencies to pin and verify. [D1], [D2] |

Avalonia's current matrix lists Android 16 as Tier 1, Android 12–15 as Tier 2, and Android 11 or earlier as Tier 3/commercial support only. This is a support-policy distinction, not proof that an older device cannot execute an app. The user has now confirmed Android 16; the matrix's Tier 1 Android 16 targets are ARM64 and x64. It currently states .NET 10 for Android; exact SDK/package versions and device compatibility still need validation. Linux distribution/version also affects tier. Linux defaults to X11/XWayland, with an opt-in native Wayland backend in current releases. [A1], [A9]

## Android storage and bookmark evidence

Avalonia's storage documentation explicitly says Android paths may be `content:` URIs. Read through `IStorageFile.OpenReadAsync()` and enumerate through `IStorageFolder.GetItemsAsync()`; a raw path or `TryGetLocalPath()` is not a portable file-access contract. Save/reopen bookmarks for later access, and handle denial or invalidation. Desktop bookmarks can simply represent paths, so their semantics differ from Android grants. [A3], [A4], [A5]

This was checked against official Avalonia **12.1.4** source:

- The Android folder picker invokes `ACTION_OPEN_DOCUMENT_TREE`. Restoring a folder bookmark decodes its URI and constructs a storage object. [A6]
- Folder enumeration queries `ContentResolver`; file reads open content streams. Saving a bookmark calls `TakePersistableUriPermission` and encodes the URI. These are existing framework integrations, not proposed application code. [A7]

Android's own rules still apply: grants cover user-selected locations; top-level storage/Download tree selection is restricted on Android 11+; persisted grants can cease working after files move or disappear. **Inference:** successful bookmark decoding is not sufficient evidence of usable access—enumeration or reading can still fail. Real-device tests must cover restart, revoked grants, read-only/provider cases, and rescanning. [G1], [A6], [A7]

## Build and skill implications

**Facts:** Avalonia Windows development needs the .NET SDK for its basic Win32 backend; Linux needs native platform libraries; Android adds the .NET Android workload, Android SDK, and compatible JDK. Tauri adds Rust and platform development tools, plus a frontend toolchain for React and Android SDK/NDK tooling for mobile. Neither produces one universal binary. [A1], [A8], [A9], [T5]

**Engineering preference to discuss, not a decision:**

- Avalonia makes the shared domain/import/query core use the user's existing C# knowledge. Avalonia views, styling, and mobile adaptation are still framework learning; C# experience alone does not establish UI familiarity.
- Tauri preserves the user's React UI experience while adding Rust for a portable core and potentially Kotlin for the Android folder/grant bridge. That can be worthwhile learning, but it is additional work rather than a requirement imposed by the product.
- In either design, keep AO3 work identity, downloaded-version comparison, reading status, personal notes, and queries independent of file-location handling. Pass EPUB bytes/streams through the source interface. Package local persistence within the application; synchronization remains separate work.
- A desktop C# or Python command-line sidecar is **not** a demonstrated Android reuse strategy. This research supports an in-process shared Rust core or an in-process shared C# core; it has not validated embedding a second desktop runtime/process on Android. Python can remain useful for development tooling without becoming a production dependency.

## Brief alternatives check

| Alternative | What current official sources establish |
|---|---|
| .NET MAUI | Microsoft's platform list includes Android, iOS, Mac Catalyst, and Windows; it does not provide an official Linux target. C# familiarity does not fill that Linux gap. [M1] |
| React Native | Windows is listed as a partner/out-of-tree platform; Linux is represented by a community renderer. Assess those implementations and their plugin coverage independently rather than infer all-three support from React familiarity. [R1] |
| Wails/Go | Current Wails v3 docs do include Android, but mark it experimental. Its Android file picker imports cache copies, while directory/save dialogs are unsupported. It is not accurately described as desktop-only, but adds mobile maturity and folder-access questions. [W1] |

Before choosing between the two main candidates, validate the same small workflow on Linux and Android: select a collection location, enumerate and open an EPUB copy, extract metadata, reopen local persisted data, then restore access after restart. Windows packaging, large-collection behavior, native SQLite assets, and a future reader integration remain untested. This is proposed evidence to gather, not a request to implement a prototype yet.

[A1]: https://docs.avaloniaui.net/docs/supported-platforms "Avalonia supported platforms and support tiers"
[A2]: https://docs.avaloniaui.net/docs/fundamentals/cross-platform-architecture "Avalonia shared core, UI and platform entry projects"
[A3]: https://docs.avaloniaui.net/docs/services/storage/storage-provider "Avalonia storage provider platform compatibility"
[A4]: https://docs.avaloniaui.net/docs/services/storage/storage-item "Avalonia content URIs, streams and folder enumeration"
[A5]: https://docs.avaloniaui.net/docs/services/storage/bookmarks "Avalonia bookmark lifecycle and platform differences"
[A6]: https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Android/Avalonia.Android/Platform/Storage/AndroidStorageProvider.cs "Avalonia 12.1.4 Android folder picker and bookmark restoration"
[A7]: https://github.com/AvaloniaUI/Avalonia/blob/12.1.4/src/Android/Avalonia.Android/Platform/Storage/AndroidStorageItem.cs "Avalonia 12.1.4 persisted URI grants, folder queries and content streams"
[A8]: https://docs.avaloniaui.net/docs/platform-specific-guides/android "Avalonia Android build setup"
[A9]: https://docs.avaloniaui.net/docs/platform-specific-guides/linux "Avalonia Linux backend and platform libraries"
[T1]: https://v2.tauri.app/ "Tauri 2 target platforms and shared languages"
[T2]: https://v2.tauri.app/plugin/dialog/ "Tauri dialog plugin Android folder limitation and content URIs"
[T3]: https://v2.tauri.app/develop/plugins/develop-mobile/ "Tauri Kotlin/Java mobile integration"
[T4]: https://v2.tauri.app/plugin/sql/ "Tauri local SQLite option and target matrix"
[T5]: https://v2.tauri.app/start/prerequisites/ "Tauri platform toolchains"
[D1]: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/ "Microsoft.Data.Sqlite overview"
[D2]: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/custom-versions "SQLite native bundles for .NET"
[G1]: https://developer.android.com/training/data-storage/shared/documents-files "Android Storage Access Framework and persistent grants"
[M1]: https://learn.microsoft.com/en-us/dotnet/maui/supported-platforms "Microsoft's official .NET MAUI platform list"
[R1]: https://reactnative.dev/docs/out-of-tree-platforms "React Native partner and community platform implementations"
[W1]: https://wails.io/guides/mobile/android/ "Wails v3 experimental Android support and file-dialog limitations"

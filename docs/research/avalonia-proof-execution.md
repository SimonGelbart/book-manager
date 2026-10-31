# Avalonia/.NET capability proof: execution record

Date: 2026-10-31. Direction: [ADR 0001](../adr/0001-avalonia-and-shared-dotnet-core.md). Scope: the [first capability proof](../architecture/cross-platform-proposal.md#first-capability-proof).

## Current result

**Blocked before compilation in the agent workspace.** No application prototype has been generated, restored, built, or run. There is no target-device result yet.

The official installer downloaded and installed .NET SDK **10.0.403** and runtime **10.0.14** into an isolated workspace directory. `dotnet --info` listed those installed versions, but managed SDK startup failed. `dotnet --version` also failed with exit code **137** and:

```text
Failed to create CoreCLR, HRESULT: 0x8007000E
```

Setting an explicit GC heap limit and disabling runtime diagnostics did not resolve startup. The workspace has no `/proc` or `/sys` mount; syscall tracing is prohibited. These are observations, not a diagnosed cause. No system installation or privileged workaround was attempted. This failure does not establish whether .NET or Avalonia runs on the user's Pop!_OS system.

The installer, downloaded SDK, caches, and private EPUB remain outside the Git repository. The [toolchain research](validation-toolchain.md) records candidate versions and published artifact evidence separately from execution.

## Next executable slice

Use a development environment where `dotnet --info` and `dotnet --version` complete successfully. Confirm the chosen SDK before generating a probe under `prototypes/avalonia-dotnet-proof/`:

```sh
dotnet new install Avalonia.Templates@12.1.4
dotnet new avalonia.xplat --help
```

Use the cross-platform template's desktop and Android entry projects with shared UI; leave other platforms outside the proof. Add a target-neutral C# metadata/application module accepting an EPUB stream, and a local SQLite catalogue behind its own interface. Both hosts must call the same implementation. The template alone cannot demonstrate this behavior.

Build one vertical slice: choose a folder, enumerate EPUBs recursively, read one EPUB's metadata, save a reading-status change and personal note, then recover both after restarting the app. Use synthetic metadata fixtures in Git. Run the user's sample locally without committing its identity, tags, or narrative.

| Check | Status and required evidence |
|---|---|
| Shared core and SQLite | Not implemented. Exercise extraction through the public application interface; close/reopen the database and verify the saved status/note. |
| Pop!_OS 24.04 | Not run. Record the actual display session, launch the desktop app, select a folder, enumerate subfolders, and import an EPUB. |
| Android 16 | Not built or run. Install the pinned Android workload and required SDK/JDK, build the Android head, then use a real document-tree grant to enumerate and read an EPUB. |
| Android retained access | Not run. After app restart and device restart, enumerate and read again through the saved grant. Reopening a bookmark object is insufficient. |
| Windows | Not built or run. Record a separate build/package result; runtime remains unverified until run on Windows. |

Capture the resolved package graph, commands, and observed results as the proof progresses. Full collection import, duplicate/version rules, filtering, ratings, synchronization, and a reader remain outside this probe.

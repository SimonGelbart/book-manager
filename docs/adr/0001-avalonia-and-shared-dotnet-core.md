---
status: accepted
date: 2026-10-31
---

# Use Avalonia and a shared .NET/C# core

The project starts on Pop!_OS 24.04, prefers Windows compatibility, and must keep a future Android collection manager practical without requiring a hosted server. The user chose Avalonia and .NET after comparing it with React/Tauri and Flutter, favoring their existing C# experience and a shared local implementation across the target platforms.

Keep collection and reading logic in a C# core independent of Avalonia and platform types. Desktop and Android entry projects wire that core to the UI and file-access adapters; Android document references remain opaque outside the adapter.

This accepts the architecture direction, not a claim that the targets have passed validation. The [import-and-persistence capability proof](../architecture/cross-platform-proposal.md#first-capability-proof) remains open, including Pop!_OS graphics and picking, Android retained folder access, native SQLite, and a Windows build. Revisit this ADR if the proof exposes a blocking limitation. Exact dependency pins and final packaging are still to be validated.

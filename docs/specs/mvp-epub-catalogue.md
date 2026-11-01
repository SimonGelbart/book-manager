# MVP specification: personal EPUB catalogue

Date: 2026-11-01. Product scope: confirmed by the owner after the discovery interview. Specification status: **draft for review of the proposed testing boundary**. This document specifies intended behavior; no application implementation or successful platform proof exists yet.

## Problem Statement

The owner has thousands of downloaded EPUBs across their computer and phone, initially mainly AO3 works. Folders and filenames do not provide a useful view of what they have read, their personal notes and assessments, duplicate copies, or downloaded updates to previously read works.

They need to catalogue the existing collection, find a suitable next read, and retain their reading information independently of the files. Their first target is Pop!_OS 24.04. They prefer Windows compatibility and want the architecture to support a later Android collection manager. The application must work locally without a hosted server and must also support ordinary non-AO3 EPUBs.

## Solution

Build an Avalonia desktop application with a shared .NET/C# core. The owner selects existing folders and manually scans or rescans them, including subfolders. The catalogue presents one entry per work, with downloaded versions and EPUB copies grouped beneath it. Files remain in their original locations.

Provide basic EPUB metadata and optional richer AO3 extraction, title/author search, combined filters, word-count sorting, personal reading status, ratings, Likes, and one editable note per work. Show which downloaded versions have been read and which imported updates remain unread. Open a selected copy in the operating system's associated reader.

Keep work identity, metadata, and personal reading information independent of a source provider or file location. Missing files retain their catalogue information. Uncertain associations and version order are visible rather than silently guessed.

### Acceptance examples

These are observable product outcomes, independent of the internal class or database structure.

| ID | Scenario | Required outcome |
|---|---|---|
| AC01 | Scan a selected folder containing EPUBs in subfolders, then scan it again unchanged. | Discover the files recursively and retain the same work/version/copy associations on the unchanged rescan. The default status for a newly catalogued work is Unknown. |
| AC02 | Import an ordinary EPUB without an AO3 link or ID. | Create a normal catalogue entry using available metadata. Reading status, notes, ratings, and Likes are available under their normal rules; absence of AO3 identity alone does not put it in Needs review. |
| AC03 | Import an AO3 EPUB with categorized preface metadata. | Preserve the available tag categories, reported word count, chapter information, and source identity. Keep source content ratings separate from personal ratings and author notes separate from personal notes. |
| AC04 | Scan two copies of the same work and chapter text, with only cover/font/layout differences. | Show one work and one downloaded version, retain both file locations, and show the duplicate indicator. Cosmetic differences do not create unread-update indicators. |
| AC05 | Import a version with edits to existing chapter text but the same chapter count. | Retain it as a different downloaded version under the same work. Chapter-count equality does not make it an identical duplicate. |
| AC06 | A ten-chapter version is marked Read; a twelve-chapter version is then imported. | Keep Read and the earlier reading information. Show 10 chapters read, 12 available, and 2 new chapters; include the work in Updates to read. |
| AC07 | Mark that twelve-chapter version read. | Record it as read, retain the earlier read-version information, and clear its unread update. Remove the work from Updates to read only when no known unread update indicator remains. |
| AC08 | A read ten-chapter version receives a newer text revision with no added chapters. | Keep the personal status and read-version history; show Text revised until the revised version is explicitly marked read. |
| AC09 | Import an older copy after a newer downloaded version. | Retain the older copy without replacing the catalogue's newer metadata merely because the older copy was scanned later. |
| AC10 | Available evidence cannot reliably establish version order. | Flag the uncertainty and let the owner choose which version supplies catalogue metadata. Preserve all versions and reading information. |
| AC11 | All known copies of a work are missing after a successful rescan of their source. | Show Files missing and retain metadata, last-known locations, versions, personal reading information, rating, Like, and note. An unsuccessful or incomplete scan does not prove absence. |
| AC12 | Save personal information, close the application, and reopen it. | Recover the saved catalogue and personal information. Rescans and additional copies do not reset the owner's recorded information. |
| AC13 | Require tags A and B, exclude tag C, set reading status and a word-count range. | Return works containing both A and B, containing no C, and satisfying the other active filters. Search and Updates to read can further narrow the same results. |
| AC14 | Browse a work whose selected metadata source has no reported word count. | Display Unknown, preserve normal catalogue/reading features, and do not calculate a count or substitute zero. Known counts use the metadata source chosen for the catalogue. |
| AC15 | Set a personal rating on a Read or Dropped work. | Permit no rating or one of 1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, and 5. Like is independently available at any reading status. |
| AC16 | Work identification or association is genuinely ambiguous. | Retain the EPUB location and available metadata in Needs review without an uncertain automatic merge of personal information. Let the owner identify it as a new work or associate it with an existing work. Preserve existing personal information. Needs review is separate from the reading status Unknown. |
| AC17 | Open a selected available EPUB copy in the default desktop reader. | Request launch of that copy without changing reading status or marking it read. If launch or access fails, report the problem and preserve catalogue information. |

## User Stories

1. As the collection owner, I want to select my existing EPUB folders, so that I can catalogue downloads without reorganizing files first.
2. As the collection owner, I want scans to include subfolders, so that my current folder structure is usable.
3. As the collection owner, I want to run Scan or Rescan manually, so that I control when the collection is refreshed.
4. As the collection owner, I want unchanged rescans to retain existing associations, so that scanning does not create repeated catalogue entries.
5. As the collection owner, I want to import ordinary EPUBs, so that the application is useful beyond AO3.
6. As the collection owner, I want each work to have its own catalogue identity, so that personal information does not depend on a provider ID.
7. As the collection owner, I want to see the available title, authors, and descriptive metadata, so that I can recognize my downloads.
8. As the collection owner, I want AO3 fandoms and categorized tags extracted when available, so that I can browse without re-entering them.
9. As the collection owner, I want AO3 completion information kept separate from my reading status, so that publication state does not misrepresent my progress.
10. As the collection owner, I want optional metadata to remain optional, so that a missing source-specific field does not prevent ordinary use.
11. As the collection owner, I want one catalogue entry per work, so that multiple files do not clutter the main collection.
12. As the collection owner, I want to see each copy's location, so that I can find the EPUB I already have.
13. As the collection owner, I want a duplicate indicator, so that I know when additional copies of the same version exist.
14. As the collection owner, I want cosmetic EPUB variants grouped as duplicates, so that cover or layout changes do not look like new reading content.
15. As the collection owner, I want chapter additions recognized as another downloaded version, so that I can distinguish an update from a duplicate.
16. As the collection owner, I want edits to existing chapter text recognized even at the same chapter count, so that revisions remain distinguishable.
17. As the collection owner, I want older versions retained under their work, so that I can inspect earlier snapshots.
18. As the collection owner, I want the newest version to supply catalogue metadata, so that browsing and filtering reflect the freshest known information.
19. As the collection owner, I want importing an older copy later to preserve newer metadata, so that scan order does not cause a rollback.
20. As the collection owner, I want uncertain version order flagged with a metadata-source choice, so that I can resolve an ambiguous case myself.
21. As the collection owner, I want ambiguous identifications kept for review, so that unrelated works and personal information are not silently merged.
22. As the collection owner, I want new works to start as Unknown, so that existing downloads are not assumed unread.
23. As the collection owner, I want Unknown, Unread, Reading, Read, and Dropped statuses, so that I can describe my own progress.
24. As the collection owner, I want my status shared across a work's copies, so that a duplicate import does not create a separate reading state.
25. As the collection owner, I want an optional half-star personal rating on Read or Dropped works, so that I can record my assessment without making a score mandatory.
26. As the collection owner, I want an independent Like button, so that I can flag a work I like at any reading status.
27. As the collection owner, I want one editable personal note per work, so that observations stay together across copies and versions.
28. As the collection owner, I want imported author notes kept distinct from my note, so that the application does not confuse authorship with my observations.
29. As the collection owner, I want to see which downloaded versions I have marked read, so that my reading history remains meaningful after updates.
30. As the collection owner, I want new chapters shown alongside the chapters already read, so that I can see how much I am missing.
31. As the collection owner, I want a read work to retain Read when an update arrives, so that my earlier reading is preserved.
32. As the collection owner, I want Text revised shown for an unread revision, so that I can distinguish it from additional chapters.
33. As the collection owner, I want Mark this version read, so that I explicitly record catching up.
34. As the collection owner, I want other known unread updates to remain indicated after marking an older version read, so that available reading is not hidden.
35. As the collection owner, I want Updates to read filtering, so that I can quickly find works to catch up on.
36. As the collection owner, I want title and author search, so that I can locate a specific work.
37. As the collection owner, I want fandom, tag, reading-status, completion-status, and word-count filters, so that I can choose a next read from my collection.
38. As the collection owner, I want all required tags and no excluded tags, so that filtered results satisfy my selections.
39. As the collection owner, I want active filters combined with AND, so that narrowing the collection has a predictable meaning.
40. As the collection owner, I want optional word-count minimum and maximum bounds, so that I can browse a suitable length range.
41. As the collection owner, I want shortest-first and longest-first ordering, so that I can explore works by length.
42. As the collection owner, I want Unknown for an absent reported word count, so that missing information is not shown as a measured value.
43. As the collection owner, I want to open a selected copy in my existing desktop reader, so that choosing a work leads to reading it.
44. As the collection owner, I want opening an EPUB to leave my status unchanged, so that reading records remain deliberate.
45. As the collection owner, I want missing copies to retain all catalogue information, so that moving or deleting files does not erase my reading history.
46. As the collection owner, I want failed or incomplete scans distinguished from confirmed missing files, so that inaccessible storage does not falsely remove my collection.
47. As the collection owner, I want saved information recovered after restart, so that the catalogue is durable between sessions.
48. As the collection owner, I want the application to work locally on my Linux computer, so that managing my collection does not require a hosted service.
49. As the collection owner, I want the shared core to support a later Android app, so that the desktop MVP does not force a rewrite of collection and reading logic.
50. As the collection owner, I want to resolve an import review by identifying a new work or selecting an existing work, so that uncertain files can finish joining my catalogue without an automatic merge.

## Implementation Decisions

### Accepted direction

- Follow ADR 0001: Avalonia presentation and a shared .NET/C# implementation. The initial desktop runtime target is Pop!_OS 24.04. Windows compatibility is preferred; Android collection management is a later product release, with an early capability proof to validate runtime and file access.
- Follow ADR 0002: internal Work identity, optional external source identifiers, and optional AO3 enrichment. Treat a package identifier, source identifier, downloaded version, and local file location as distinct concepts. Source identifiers are evidence for matching, not a requirement for a work to exist.
- Follow ADR 0003: Clean Architecture without CQRS. Domain is independent; Application depends on Domain and owns the adapter interfaces; Infrastructure and Presentation depend inward, and hosts compose the concrete implementations. Use ordinary Application method calls for lookups and changes, with one catalogue design and persistence model rather than command/query handler pipelines or split read/write stores.
- Use the owner-required repository-root `src/` directory for production projects and runtime-proof code, and `tests/` for automated test projects, helpers, and committed synthetic fixtures. Concrete project names and the testing framework are still to be selected.
- Separate presentation, application coordination, domain behavior, EPUB extraction, catalogue persistence, platform integrations, and runner composition. The shared collection and reading implementation must not require Avalonia or operating-system types.
- Keep one application-facing boundary for scan/import, catalogue queries and details, resolving import review, personal-record changes, marking versions read, choosing a metadata source, and requesting external-reader launch. Adapter contracts supply source references, enumeration, readable EPUB data, and platform outcomes.
- Keep file locations opaque to the shared core. Both desktop files and Android permission-granted documents must feed the same extraction/application implementation. Do not require a desktop-only sidecar for the phone.
- Use base EPUB extraction and optional source-specific enrichment. Preserve available metadata, categorized AO3 tag values, and extraction diagnostics. Do not interpret package UUIDs as AO3 IDs, split tag values on commas, or infer chapter totals from the number of spine documents.
- Keep Works, Downloaded versions, EPUB copies, and personal reading information separately identifiable in persistence. Retain metadata per version and locations per copy. Store read-version information without requiring a full rereading journal or passage-level progress tracking.
- Import and rescan must preserve personal information. Changes to imported metadata do not replace personal notes, ratings, Likes, or deliberate reading records.
- Keep the catalogue local and durable. Embedded SQLite is the validation candidate; the provider, native assets, and close/reopen behavior still require proof on the executed platforms. No hosted backend is required for the MVP.
- Use the newest stable compatible toolchain, pinned for reproducible builds and updated deliberately. Current dependency research is a candidate baseline, not evidence of a successful restore or native runtime.
- Route external desktop reader launch through a platform adapter. Launching or failing to launch a selected copy must not update personal reading records.
- Keep unread-update detection local to imported EPUBs. A newer local snapshot can produce new chapters or Text revised indications; the application does not check AO3 for chapters not downloaded.

### Implementation defaults proposed for review

These fill small behavior gaps without adding product capabilities. They are not presented as separately confirmed interview decisions.

- Search is case-insensitive text matching over available titles and author names, narrowed by every active filter. Required tags all match; excluded tags all remain absent. Advanced user-defined OR queries remain outside scope.
- Word-count bounds are inclusive. Unknown counts do not satisfy an active numeric bound, remain visible when no numeric bound is active, and sort after known counts in either direction. Invalid bounds are reported rather than silently swapped.
- Missing metadata remains unknown or unavailable; do not infer publication completion from personal Read status. Only evidence-supported unread-update indications are shown.
- An individual unreadable or malformed EPUB produces a visible scan diagnostic while other readable files can still be processed. A source scan that cannot establish absence must not mark unverified locations missing.
- Reconnect moved or returned copies when identity evidence is reliable, preserving the work and personal information. Ambiguous cases go to review. The comparison and reconciliation algorithms must be validated before their implementation tickets are considered ready.
- Preserve an existing personal rating if the user later changes reading status outside Read/Dropped; restrict assigning or editing a rating to eligible statuses. This avoids discarding an earlier assessment and remains a reviewable default.

### Technical gates

1. **Runtime capability:** complete the small shared-core import-and-persistence proof in the existing validation task. Include a generic EPUB without AO3 identity, AO3 enrichment, a saved status and note, Pop!_OS source access, Android document access and retained grants, and a separate Windows build/package result.
2. **Content identity and ordering:** validate work association, normalized chapter-content comparison, cosmetic variants, text revisions, additions, metadata-only updates, and version-order evidence with representative pairs. A single inspected AO3 specimen does not validate these algorithms. Record conservative behavior for missing or conflicting evidence and preserve fresh metadata without falsely declaring a chapter-text revision.
3. **Desktop delivery:** choose and document the installation/package approach, verify native startup and default-reader handoff on Pop!_OS, and exercise restart persistence. Record Windows build and runtime results separately.
4. **Collection scale:** evaluate the implemented scan and query workflows with thousands of synthetic entries. Record dataset size, timings, memory observations, and UI responsiveness; no numeric performance budget was agreed during discovery.

These gates refine implementation readiness without reopening the agreed product scope. Build failures or target limitations must remain visible and may require revisiting an ADR.

## Testing Decisions

### Proposed primary boundary — owner confirmation pending

Test externally observable behavior through the **shared catalogue application interface**, the highest boundary common to the desktop and future phone hosts. There is no existing implementation or test framework to preserve; this proposes the new public boundary before tests are written.

Supply synthetic EPUB streams and source references through the real adapter contract, exercise application requests, and assert returned collection/query state and persisted results. Use a temporary real local database for persistence behavior and reopen the Application module and catalogue to verify durability. Keep UI widget types, SQL statements, parser helpers, private methods, and internal call sequences out of behavior assertions.

This proposal is compatible with the accepted Clean Architecture: the test composition supplies real Infrastructure adapters to Application-owned interfaces. Application itself does not reference Infrastructure, and the tests do not depend on CQRS handlers. The owner has not yet confirmed the proposed primary testing interface or selected a testing framework.

Use controlled source-access and reader-launch outcomes at the platform boundary for application tests. These can verify which selected copy was requested and how a failure is surfaced; they cannot establish actual operating-system launch or permission behavior.

### Coverage and evidence

- Cover AC01–AC17 at the application boundary where feasible, including repeated scans, missing metadata, uncertain associations, numeric/rating limits, explicit read-version changes, AND filters, duplicate/version grouping, and restart persistence.
- Exercise base EPUB extraction, AO3 enrichment, catalogue persistence, queries, and domain reading rules together through that boundary. Prefer smaller targeted component tests only where a public behavior failure cannot be diagnosed effectively at that level; do not duplicate every scenario at every layer.
- Generate synthetic metadata/content fixtures for generic EPUBs, split AO3 prefaces, same-text cosmetic variants, added chapters, same-count revisions, missing counts, and ambiguous identifiers/order. Tests must not require the owner's private EPUB or a live AO3 connection.
- Keep the owner's specimen and its identifying values out of public fixtures, reports, and committed test output. Existing specimen observations guide fixture structure, not copied narrative or metadata.
- Use target-platform smoke checks for native startup, folder pickers, real stream access, SQLite native loading, and default-reader handoff. Android retained-access checks must enumerate and read after app and device restarts; reopening a bookmark object alone is insufficient.
- Treat a Windows build, a Linux build, a headless UI check, and an actual target-device run as different evidence. Android proof does not imply a shipped Android MVP, and a build does not establish graphics, permissions, or reader launch.
- Prior art: the repository contains planning documents and metadata/runtime investigations but no application code, automated tests, or successful prototype run. No test library or exact API signature has been selected.
- Current execution limit: the agent workspace installed an SDK but could not start managed SDK commands. No application build, automated behavior test, or target-device result is claimed by this specification.

## Out of Scope

- Shipping the full Android collection manager in the desktop MVP.
- Synchronization between devices or a hosted service.
- Online AO3 update checking or downloads.
- An integrated reader, chapter/passage annotations, or reader-progress integration.
- Physical file reorganization or automatic deletion of duplicate EPUBs.
- Automatic folder watching.
- Advanced user-defined OR filtering.
- Automatically calculating word counts from EPUB text when a reported count is absent.
- A general EPUB editor or additional source integrations beyond the basic reader and AO3 enrichment required here.

## Further Notes

### Scope and review status

The owner confirmed on 2026-11-01 that nothing essential was missing from the discovered MVP. Product discovery is complete for this first specification. The proposed primary testing boundary needs the `to-spec` workflow's expectation check before this draft is marked ready for implementation planning; the small implementation defaults above are explicitly visible for review.

After that check, break the specification into dependent implementation tasks, keeping capability and identity validation ahead of features that depend on their results. Keep the source documents and focused Git history as the record of how decisions evolved.

### Reference documents

- [Product brief and discovery record](../planning/product-brief.md)
- [Domain glossary](../../CONTEXT.md)
- [ADR 0001: Avalonia and shared .NET core](../adr/0001-avalonia-and-shared-dotnet-core.md)
- [ADR 0002: source-independent identity](../adr/0002-source-independent-work-identity.md)
- [ADR 0003: Clean Architecture without CQRS](../adr/0003-clean-architecture-without-cqrs.md)
- [Architecture outline](../architecture/cross-platform-proposal.md)
- [EPUB specimen findings and limits](../research/epub-metadata-validation.md)
- [Candidate toolchain](../research/validation-toolchain.md)
- [Runtime proof execution record](../research/avalonia-proof-execution.md)
- [Runtime capability task #1](https://github.com/SimonGelbart/book-manager/issues/1)

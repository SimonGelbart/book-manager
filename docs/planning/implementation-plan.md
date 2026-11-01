# Tome Raider — implementation tickets

Date: 2026-11-01. Status: **owner-approved and published** using Matt's `to-tickets` workflow. The owner approved the breakdown and dependency order. The [MVP specification](../specs/mvp-epub-catalogue.md) and Application test seam are approved; nine new task issues were published, reusing the existing runtime gate.

Parent: [specification issue #2](https://github.com/SimonGelbart/book-manager/issues/2). **T1–T10 are plan aliases**, not GitHub issue numbers. T1 reuses the existing [runtime proof issue #1](https://github.com/SimonGelbart/book-manager/issues/1). All ten task issues are labeled `ready-for-agent`; agents must respect the blocking links before starting dependent work.

## Published issue mapping

The GitHub connector exposes issue creation and labels but not native dependency relationships. Each new issue therefore records its real blocking issue links in a **Blocked by** section.

| Plan alias | GitHub task | Blocked by |
|---|---|---|
| T1 | [#1 — Validate Avalonia/.NET import and persistence on target platforms](https://github.com/SimonGelbart/book-manager/issues/1) | No issue blocker; SDK/device prerequisites apply |
| T2 | [#3 — Durable folder catalogue and metadata](https://github.com/SimonGelbart/book-manager/issues/3) | [#1](https://github.com/SimonGelbart/book-manager/issues/1) |
| T3 | [#4 — Work association and import review](https://github.com/SimonGelbart/book-manager/issues/4) | [#3](https://github.com/SimonGelbart/book-manager/issues/3) |
| T4 | [#5 — Versions, duplicates, and metadata source](https://github.com/SimonGelbart/book-manager/issues/5) | [#4](https://github.com/SimonGelbart/book-manager/issues/4) |
| T5 | [#6 — Personal reading records](https://github.com/SimonGelbart/book-manager/issues/6) | [#4](https://github.com/SimonGelbart/book-manager/issues/4) |
| T6 | [#7 — Read versions, unread updates, and catch-up](https://github.com/SimonGelbart/book-manager/issues/7) | [#5](https://github.com/SimonGelbart/book-manager/issues/5), [#6](https://github.com/SimonGelbart/book-manager/issues/6) |
| T7 | [#8 — Search, filters, and length ordering](https://github.com/SimonGelbart/book-manager/issues/8) | [#7](https://github.com/SimonGelbart/book-manager/issues/7) |
| T8 | [#9 — Rescan reconciliation and retained information](https://github.com/SimonGelbart/book-manager/issues/9) | [#7](https://github.com/SimonGelbart/book-manager/issues/7) |
| T9 | [#10 — Default desktop reader handoff](https://github.com/SimonGelbart/book-manager/issues/10) | [#3](https://github.com/SimonGelbart/book-manager/issues/3) |
| T10 | [#11 — Desktop delivery and collection-scale validation](https://github.com/SimonGelbart/book-manager/issues/11) | [#8](https://github.com/SimonGelbart/book-manager/issues/8), [#9](https://github.com/SimonGelbart/book-manager/issues/9), [#10](https://github.com/SimonGelbart/book-manager/issues/10) |

## Approved breakdown

1. **T1 — Prove the runtime and local persistence.** **Blocked by:** no issue; requires a development environment where the SDK starts and access to the target platforms. **Delivers:** a minimal folder-to-EPUB-to-saved-status/note workflow through the same C# implementation on the executed desktop/Android hosts. Existing issue #1 is the gate.
2. **T2 — Catalogue existing folders with durable metadata.** **Blocked by:** T1. **Delivers:** recursive scans, a basic collection/details view, generic EPUB import and AO3 metadata enrichment, with unchanged rescans retaining records. Establish the executable TUnit and architecture-check workflow in this first production slice.
3. **T3 — Associate works and resolve uncertain imports.** **Blocked by:** T2. **Delivers:** one work entry for confidently associated copies, plus a Needs review flow for ambiguous cases that the owner can resolve to an existing or new work.
4. **T4 — Distinguish versions, duplicates, and current metadata.** **Blocked by:** T3. **Delivers:** copy/version details, cosmetic-duplicate grouping, real text revisions/additions, fresh catalogue metadata, and a manual metadata-source choice when order is uncertain.
5. **T5 — Record personal reading information.** **Blocked by:** T3. **Delivers:** all five statuses, optional eligible personal ratings, independent Likes, and one editable note, shared across a work's associated copies and recovered after restart.
6. **T6 — Show unread updates and record catching up.** **Blocked by:** T4 and T5. **Delivers:** read-version history, new chapters/Text revised indicators, Mark this version read, and the Updates to read filter.
7. **T7 — Find the next read with search and filters.** **Blocked by:** T6. **Delivers:** title/author search, the full agreed AND filter set, word-count bounds and sorting, combined with Updates to read.
8. **T8 — Reconcile missing, moved, and returned copies.** **Blocked by:** T6. **Delivers:** truthful file-availability results after rescans, reliable reconnection, ambiguity review, and complete retention of catalogue/personal information.
9. **T9 — Open a selected copy in the desktop reader.** **Blocked by:** T2. **Delivers:** working default-reader handoff for a selected copy, with understandable failure results and unchanged reading records.
10. **T10 — Package and validate the desktop MVP at collection scale.** **Blocked by:** T7, T8, and T9. **Delivers:** a documented Pop!_OS installation/package, an exercised full workflow with thousands of entries, passing test/architecture checks, and clearly scoped Windows compatibility evidence.

T4 and T5 can proceed independently after T3. T9 can proceed after T2. T7 and T8 can proceed independently after T6. The published blocking links preserve these edges; work only the frontier whose blocking tasks are complete.

## Shared delivery rules

- Every production slice includes a visible or directly verifiable workflow through presentation, Application, relevant Domain behavior, and persistence/platform adapters as needed. Avoid separate tickets that only create all entities, all repositories, or all views.
- Preserve Clean Architecture without CQRS, the source/test roots, source-independent Work identity, optional AO3 enrichment, and the TomeRaider identifier prefix.
- Use TUnit through the agreed Application interface, temporary real SQLite files for persistence behavior, and architecture checks. Use Testcontainers only where an implemented integration requires containerized dependencies.
- Use synthetic fixtures and keep the owner's private EPUB and identifying values out of committed assets and output.
- The existing capability proof remains a validation gate, not evidence of a shipped Android MVP. Record actual executed platforms; compilation alone does not establish picker, grants, native rendering, or reader behavior.
- Each affected ticket must make the specification's proposed engineering defaults explicit in its acceptance criteria before implementation. Record meaningful technical decisions and evidence rather than silently treating unvalidated matching heuristics as facts.
- Prefer evolving the proven implementation when suitable; a throwaway proof does not by itself satisfy production slice acceptance.

## Ticket acceptance details

### T1 — Runtime and persistence proof (reuse issue #1)

**What to build:** the already scoped minimal workflow, proving that the chosen local implementation and dependencies can support the intended hosts.

**Blocked by:** no issue. The current agent workspace's SDK-startup failure remains an environment prerequisite to resolve in a suitable development environment.

- Record a runnable SDK, workload and resolved package set; do not mark the current workspace startup failure resolved without a successful command.
- Exercise generic EPUB import without AO3 identity and an AO3 enrichment case through the shared implementation.
- Select/read a source and save/recover one status and note on Pop!_OS and Android as described in the existing issue.
- Verify Android enumeration/read after app and device restart, and record Windows build evidence separately.
- Preserve the existing issue's narrow scope and update its execution record with observed outcomes during implementation.

### T2 — Durable folder catalogue and metadata

**What to build:** select an existing folder, scan distinct works recursively, browse available metadata, and recover the catalogue after restart.

**Blocked by:** T1.

- Provide a basic collection and work-details view backed by the Application interface and a durable local catalogue.
- Import generic EPUB metadata and available AO3 categorized tags, reported statistics, source link, and descriptive fields. Keep author notes and personal notes separate; support split prefaces.
- Use internal work identity; missing AO3 fields or word counts do not prevent ordinary import. New works start Unknown and absent word counts display Unknown.
- Rescanning an unchanged corpus retains work/copy associations and existing personal information; report individual file errors without treating an incomplete scan as proof of absence.
- Establish compatible TUnit discovery/execution, a real temporary-database test, and required architecture checks in the automated workflow/CI. Prove that an intentional forbidden dependency fails, and that empty selections cannot pass silently.

Acceptance anchors: AC01–AC03, AC12, AC14; implementation architecture/testing requirements.

### T3 — Work association and import review

**What to build:** group confidently associated copies beneath a work and allow the owner to finish identifying ambiguous imports.

**Blocked by:** T2.

- Validate and record the source-identifier/matching policy before applying it automatically. An EPUB package identifier alone does not establish the underlying Work.
- Keep unrelated or uncertain candidates separate; absence of an AO3 link alone does not trigger review for an ordinary EPUB.
- Show location and available metadata in Needs review and support identifying a new work or associating with an existing work.
- Retain existing catalogue and personal information on resolution; do not silently merge conflicting personal records.
- Demonstrate the flow through the UI and public Application interface using synthetic reliable and conflicting identity cases.

Acceptance anchors: AC02, AC16; work identity and copy-location stories.

### T4 — Versions, duplicates, and metadata source

**What to build:** inspect a work's copies/versions, distinguish real reading-content changes from cosmetic duplicates, and browse the freshest supported metadata.

**Blocked by:** T3.

- Validate representative content pairs before relying on the comparison method: exact copies, cosmetic variants, chapter additions, equal-count text revisions, and metadata-only updates.
- Show one downloaded version for same-text cosmetic variants, preserve each copy location, and display the duplicate indicator.
- Keep revised text and added chapters as different versions, including revisions with unchanged chapter count.
- Retain metadata observations and use the newest supported version for the catalogue. Later import of an older copy cannot roll metadata back merely by scan order.
- Flag unsupported or conflicting version-order evidence, let the owner choose the metadata source, and preserve all versions. Metadata-only changes refresh metadata without inventing a chapter-text revision.

Acceptance anchors: AC04, AC05, AC09, AC10.

### T5 — Personal reading records

**What to build:** edit status, assessment, Like, and a personal note on a work, with the same values visible through its associated copies after restart.

**Blocked by:** T3.

- Support Unknown, Unread, Reading, Read, and Dropped without resetting the state on import/rescan.
- Allow an absent personal rating or one of the nine agreed half-star values from 1 to 5, with assignment/editing on Read or Dropped works.
- Make Like independent of status and rating, and provide one editable personal note per work.
- Make the proposed rating-retention behavior explicit: preserve an existing assessment if status becomes ineligible, while limiting rating assignment/editing to eligible statuses, or document the resolution before implementing a different rule.
- Save and recover values through the public interface with real persistence and isolated test data.

Acceptance anchors: AC12, AC15; personal-reading stories.

### T6 — Read versions, unread updates, and catch-up

**What to build:** show which version has been read, which imported content remains unread, and let the owner explicitly catch up.

**Blocked by:** T4 and T5.

- Keep Read and the earlier read-version information when a newer imported version has additional chapters or revised text.
- Demonstrate the agreed ten-read/twelve-available/two-new case and a same-count Text revised case.
- Mark this version read records the selected version and clears only the relevant unread-update state; retain earlier reading information and other known unread updates.
- Provide Updates to read filtering based on the existing indicators, without online AO3 checking.
- Preserve all behavior after restart and through unchanged rescans.

Acceptance anchors: AC06–AC08 and Updates to read stories.

### T7 — Search, filters, and length ordering

**What to build:** locate a specific work or choose a next read by narrowing and ordering the catalogue.

**Blocked by:** T6, which provides the required update state and its underlying identity/personal-record work.

- Combine title/author search with fandom, required/excluded tags, reading status, publication completion, word-count bounds, and Updates to read.
- Require all included tags, none of the excluded tags, and all active filter rules. Keep advanced user-defined OR queries outside scope.
- Provide shortest-first and longest-first ordering using the same metadata source as the details and filters.
- Make the specification's proposed query defaults explicit: case-insensitive text search, inclusive bounds, unknown counts excluded by active numeric bounds and sorted after known counts, and validation of invalid bounds.
- Verify combinations through Application-level behavior tests with missing metadata and multiple versions.

Acceptance anchors: AC13, AC14 and browsing stories.

### T8 — Rescan reconciliation and retained information

**What to build:** refresh locations/availability without losing what the owner knows about the works, even when files have moved or disappeared.

**Blocked by:** T6, so the acceptance can include real read-version history and update state.

- Validate and record reliable moved/returned-copy reconciliation using the established identity/comparison evidence; uncertain cases remain reviewable.
- After a successful source rescan, retain last-known locations and mark established missing copies appropriately. Show Files missing when all known copies are missing.
- Retain all metadata, versions/copies, statuses, read-version information, notes, ratings, and Likes.
- Distinguish failed/incomplete/inaccessible-source scans from established file absence, and handle a copy returning without creating a new unrelated work or resetting records.
- Exercise the complete flow through the UI and Application interface, with real catalogue reopen checks.

Acceptance anchors: AC01, AC11, AC12 and location/reconciliation stories.

### T9 — Default desktop reader handoff

**What to build:** select a catalogued copy and open it in the owner's associated desktop reader.

**Blocked by:** T2; the operation uses a selected copy reference and does not require content-version comparison or filtering.

- Wire the visible action through Application and a platform adapter to the operating system's associated reader.
- Preserve personal reading state and catalogue information on success or failure.
- Return useful outcomes for missing/inaccessible files and failed reader launch.
- Test requested-copy selection and error outcomes with a controlled adapter, then record real Pop!_OS handoff evidence separately. Include Windows evidence only when actually built/run there.

Acceptance anchor: AC17.

### T10 — Desktop delivery and collection-scale validation

**What to build:** provide an installable/runnable desktop MVP and demonstrate the agreed full workflow at realistic catalogue size.

**Blocked by:** T7, T8, and T9; their prerequisites cover the other MVP slices.

- Choose and document a Pop!_OS package/install approach and prerequisites, then exercise native startup, picking, import, browsing, opening a reader, saving personal information, and restart recovery.
- Run the automated TUnit behavior/integration and architecture suites, preserving the intended dependency direction and one source/test layout.
- Use a reproducible corpus with thousands of synthetic entries, recording dataset size, scan/query timings, memory observations, and UI responsiveness. Investigate material usability failures rather than inventing a previously agreed performance budget.
- Record the separate Windows build/package result and actual runtime evidence where available. Keep unexecuted Windows/Android behavior explicitly unverified.
- Review AC01–AC17 against the integrated result and document remaining limitations or resolved technical gates before considering the MVP complete.

Acceptance anchors: full MVP acceptance and delivery/scale gates.

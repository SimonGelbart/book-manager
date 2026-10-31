# Product brief

Status: discovery in progress. This document records confirmed needs and open questions; it is not an implementation specification.

## User and current problem

The project is initially for the owner's personal use. They have thousands of downloaded EPUBs on their phone and computer. The collection consists of works from Archive of Our Own (AO3).

They want to organize the collection, follow what they have read, and find something new to read among downloads they already have.

## Confirmed needs

- Classify and organize the existing collection.
- Record reading status so previously read items can be identified.
- Add personal notes.
- Make existing downloads easier to find and choose from.
- Reuse information already present in AO3 downloads, including fandoms, tags, and word counts, to reduce manual organization.
- Account for a collection of thousands of EPUBs.

## Target platforms

- The MVP starts on the computer.
- Linux is the user's current system and the initial target.
- Windows compatibility is preferred. It has not been stated as a mandatory condition for the MVP.
- A similar Android collection-management app is a long-term goal and must influence the initial architecture, even though it is outside the MVP.
- The user's current phone runs Android 16.

## MVP scope boundary

Synchronization of reading status and personal notes between phone and computer is a desired feature, but is not required for the MVP. Application form and packaging remain undecided.

OR filtering is a possible later feature and is explicitly outside the MVP.

## File handling

The MVP catalogues EPUBs in their existing folders. The user accepted this approach, with physical file reorganization left for later rather than importing copies into an application-managed folder.

The user chooses folders and manually runs Scan/Rescan to discover or refresh EPUB copies. Scans include subfolders. Automatic folder monitoring is deferred until after the MVP.

This scope decision concerns file locations. Classification and browsing within book-manager remain part of the collection-management goal.

## Work identity, duplicates, and downloaded versions

- Represent each AO3 work once in the collection, even when several EPUB copies exist.
- Share reading status and personal notes across those copies.
- Keep track of the location of each EPUB copy.
- Show a visible duplicate indicator for identical duplicate EPUB copies.
- For the MVP, distinguish updated downloaded versions from identical duplicates, such as a ten-chapter copy and a twelve-chapter copy of the same AO3 work.
- Keep different downloaded versions grouped under the same AO3 work.

Grouping applies to the catalogue; the EPUB copies remain in their existing folders. The matching method and exact criteria for identifying identical content remain open.

## Reading status and personal ratings

Use the five reading statuses defined in the [domain glossary](../../CONTEXT.md): Unknown, Unread, Reading, Read, and Dropped. Unknown covers existing downloads whose reading history has not yet been recorded. The default status on import remains undecided.

For the MVP, provide an optional personal rating for AO3 works marked Read or Dropped. The user chose a 1–5-star scale with half-star steps: 1, 1.5, 2, 2.5, 3, 3.5, 4, 4.5, and 5 stars. A work can be marked Read or Dropped without assigning a personal rating.

Also provide an optional Like button, following the user's Letterboxd-style reference. Like is available on any AO3 work, independently of reading status and personal rating, including works not yet finished or rated. Changing Like leaves reading status and the personal rating unchanged.

These are the user's own assessments, distinct from the work's imported AO3 content rating.

## Personal notes

For the MVP, provide one editable text note per AO3 work, shared across its EPUB copies and downloaded versions.

Chapter and passage annotations are deferred to the later reader extension.

## Reading updates

When a work marked Read receives a newly imported EPUB copy with additional chapters, keep the work marked Read and show a new chapters indicator. Preserve the record that the earlier downloaded version was read.

For example, a work whose ten-chapter version was marked Read stays Read when a twelve-chapter version is imported, with an indication that new chapters are available. How the user records catching up and clears the indicator remains open.

## Current constraints

- Personal use is the initial scope; a public service is not currently requested.
- The user has no server available for hosting at present. This is a current constraint, not yet an architectural decision about future hosting or synchronization.

## Development experience

The user is familiar with .NET/C#, Python, and React. They are willing to try Kotlin, Rust, or Go when there is a concrete benefit, and can learn another language if needed. No requirement to retain a specific UI framework has been stated.

## Possible later extension

The future Android app should offer similar collection-management capabilities. It could also include a reader or reader integration. Collection management is the initial priority; an integrated reader is not a first-version requirement.

## Architecture direction under discussion

The user proposed separating the frontend from the runner with adapters, and emphasized planning for Linux, Windows, and Android together. A desktop-specific implementation should not determine the shared collection and reading logic.

See the [cross-platform architecture proposal](../architecture/cross-platform-proposal.md), [runtime research](../research/cross-platform-runtime-options.md), and [runtime fit with existing skills](../research/runtime-fit-existing-skills.md). The proposal separates shared application/domain modules from platform integrations. Runtime, language, UI framework, and adapter details remain open decisions.

## Collection browsing and discovery

Support filters for fandom, reading status, AO3 completion status, and tags alongside word-count constraints. A work must contain all required tags and none of the excluded tags. Combine all active filter rules with AND for the MVP; the user chose this simpler query model.

Provide an optional Complete or Unfinished filter based on the completion status recorded in a downloaded EPUB. Keep this separate from personal reading status: a complete work can be Unread, and an unfinished work can be marked Read for the version already read.

Show the word count as part of an AO3 work's information. The count describes a downloaded version, so different versions of the same work may have different counts.

Support word-count range filtering with optional minimum and maximum values. Either bound can be used alone, both can define a range such as 10,000–50,000 words, or both can be left unset. Combine the word-count constraints with the other selected filters.

Word-count sorting remains to be clarified. When several downloaded versions exist, the choice of which version supplies the displayed and filtered metadata, including word count and AO3 completion status, is also still open.

## Metadata investigation

AO3's official source passes work tags into the ebook conversion metadata and preserves separate tag groups in the download preface. This supports investigating extraction from both the ebook metadata and the front matter, rather than assuming the metadata alone preserves tag categories.

The download preface also includes the work's reported word count, providing a source to investigate for this field.

Verified against source snapshot `aa99cd45c8b39a60944c4e038a53f5a051098b71`:

- [AO3 ebook metadata conversion](https://github.com/otwcode/otwarchive/blob/aa99cd45c8b39a60944c4e038a53f5a051098b71/app/models/download_writer.rb).
- [AO3 download preface](https://github.com/otwcode/otwarchive/blob/aa99cd45c8b39a60944c4e038a53f5a051098b71/app/views/downloads/_download_preface.html.erb).

These source-code findings do not guarantee that every existing download uses the same structure. Older downloads, third-party conversions, and edited EPUBs may differ.

One user-provided English-language EPUB 2.0 sample has now been inspected. It provides a canonical AO3 work link, categorized tags, word count, chapter information, title, author, summary, and publication/completion dates. The package identifier is a UUID rather than the canonical AO3 work identity, and the preface is split across two spine documents.

See [EPUB metadata validation](../research/epub-metadata-validation.md) for the specimen observations, primary-source locators, importer recommendations, and limits. Series extraction and comparison of duplicate or updated copies have not yet been validated against representative specimens.

## Questions still open

- What evidence is sufficient to identify identical duplicates and distinguish downloaded versions reliably.
- How to match EPUB copies to an AO3 work when identifying information is missing or inconsistent.
- How rescans should reconcile moved or missing file locations while preserving work information.
- How files currently move between devices.
- Whether any additional metadata filters are essential beyond fandom, tags, reading status, AO3 completion status, and word count.
- Whether to support word-count sorting and which downloaded version supplies the displayed and filtered metadata.
- How the user records catching up on a newer downloaded version and clears the new chapters indicator.
- Whether a reader integration means opening another Android app or reading inside book-manager.
- Installation expectations, existing tools, storage arrangements, and other fixed constraints.

## Interview record — 2026-10-31

The initial purpose is now established: manage a personal AO3 EPUB collection, track reading, add notes, and make the existing collection easier to browse. The MVP starts on the computer, targeting the user's Linux system with Windows compatibility preferred. No application stack, storage design, hosting approach, synchronization mechanism, or packaging has been chosen.

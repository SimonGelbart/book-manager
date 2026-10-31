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
- Reuse information already present in AO3 downloads, especially fandoms and tags, to reduce manual organization.
- Account for a collection of thousands of EPUBs.

## Target platforms

- The MVP starts on the computer.
- Linux is the user's current system and the initial target.
- Windows compatibility is preferred. It has not been stated as a mandatory condition for the MVP.

## MVP scope boundary

Synchronization of reading status and personal notes between phone and computer is a desired feature, but is not required for the MVP. File management, application form, and packaging remain undecided.

## Current constraints

- Personal use is the initial scope; a public service is not currently requested.
- The user has no server available for hosting at present. This is a current constraint, not yet an architectural decision about future hosting or synchronization.

## Possible later extension

An Android reading capability or integration could be useful. Collection management is the initial priority; an integrated reader is not yet a first-version requirement.

## Metadata investigation

AO3's official source passes work tags into the ebook conversion metadata and preserves separate tag groups in the download preface. This supports investigating extraction from both the ebook metadata and the front matter, rather than assuming the metadata alone preserves tag categories.

Verified against source snapshot `aa99cd45c8b39a60944c4e038a53f5a051098b71`:

- [AO3 ebook metadata conversion](https://github.com/otwcode/otwarchive/blob/aa99cd45c8b39a60944c4e038a53f5a051098b71/app/models/download_writer.rb).
- [AO3 download preface](https://github.com/otwcode/otwarchive/blob/aa99cd45c8b39a60944c4e038a53f5a051098b71/app/views/downloads/_download_preface.html.erb).

This is a source-code finding, not yet a check of the user's files or a guarantee about a deployed AO3 version. Older downloads, third-party conversions, and edited EPUBs may differ. Inspect representative downloads before fixing the importer design.

The user has offered to share representative EPUBs for this validation. Sample-file inspection is still pending.

## Questions still open

- Whether importing should index files in their existing locations or copy them into a managed collection folder.
- How files currently move between devices, and whether duplicate downloads occur.
- Which fandom and tag filters best support choosing the next item to read.
- What reading states the user needs beyond identifying already-read items.
- What personal notes should attach to: an entire item, a chapter, or a specific passage.
- Whether a reader integration means opening another Android app or reading inside book-manager.
- Installation expectations, existing tools, storage arrangements, and other fixed constraints.

## Interview record — 2026-10-31

The initial purpose is now established: manage a personal AO3 EPUB collection, track reading, add notes, and make the existing collection easier to browse. The MVP starts on the computer, targeting the user's Linux system with Windows compatibility preferred. No application stack, storage design, hosting approach, synchronization mechanism, or packaging has been chosen.

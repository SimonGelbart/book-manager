---
status: accepted
date: 2026-11-01
---

# Use source-independent work identity with optional AO3 enrichment

The initial collection and discovery focused on AO3, but the user explicitly included basic non-AO3 EPUB support in the MVP. Give every work an internal catalogue identity and retain external source identifiers as optional information; an AO3 ID is not a prerequisite for importing a work or recording personal reading information.

Keep a base EPUB metadata reader and optional source-specific enrichment behind the extraction interface. AO3 extraction supplies richer source metadata when recognized. The shared catalogue and reading logic operate on general works, downloaded versions, and EPUB copies, as defined in the [glossary](../../CONTEXT.md).

Using AO3 identity as the catalogue identity would simplify the initial source-specific case but exclude ordinary EPUBs and couple personal reading records to one provider. Internal identity requires a separate matching policy; preserve ambiguous candidates for review rather than merging uncertain associations. Missing an AO3 link alone is not an ambiguity. This decision extends [ADR 0001](0001-avalonia-and-shared-dotnet-core.md) without changing the selected runtime.

An EPUB package identifier is one piece of matching evidence rather than an assumed work identity. [EPUB Reading Systems 3.3, section 5.2](https://www.w3.org/TR/epub-rs-33/#sec-pkg-doc-pub-identifiers) advises against assuming an identifier uniquely identifies one publication, supporting this separation.

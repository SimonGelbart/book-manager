# AO3 EPUB metadata validation

Validated on 2026-10-31 against one user-provided EPUB, referred to as **sample-A**. This note records metadata observations, not an importer implementation or a completed specification. The EPUB and its identifying values are excluded from the repository.

## Primary specimen and locators

**S1** is sample-A. Namespace prefixes used below are `opf` for `http://www.idpf.org/2007/opf`, `dc` for `http://purl.org/dc/elements/1.1/`, and `x` for `http://www.w3.org/1999/xhtml`.

- **S1.P — package:** `META-INF/container.xml` identifies `content.opf` through `rootfiles/rootfile/@full-path`. Inspect `/opf:package`, its `opf:metadata`, `opf:manifest`, and `opf:spine`.
- **S1.F1 — first spine item:** resolve the first `opf:spine/opf:itemref/@idref` through the manifest. Its `x:div[@id='preface']` contains the original-posted message and `x:dl[@class='tags']`.
- **S1.F2 — second spine item:** resolve the second item in the same way. It also contains `x:div[@id='preface']`, including the title, author byline, summary, and author's notes.

These logical locators intentionally omit internal filenames that contain the AO3 work's title.

## Observations from sample-A

| Field or structure | Result | Evidence |
|---|---|---|
| EPUB package | EPUB 2.0; 13 spine items | S1.P: package `@version`; spine item count |
| Package identity | `@unique-identifier` points to a UUID identifier, not an AO3 work ID | S1.P: metadata `dc:identifier` selected by that attribute |
| AO3 work identity | A separate original-posted link contains an AO3 `/works/<numeric-id>` URL | S1.F1: `x:p[@class='message']/x:a/@href` |
| Package subjects | 31 flat `dc:subject` elements | S1.P: metadata subjects |
| Categorized tags | 30 linked tags across seven categories; counts below | S1.F1: alternating `x:dt` and `x:dd` inside the tags list |
| Word and chapter counts | Numeric word count and a downloaded/total chapter pair are present; the reported chapter pair is 10/10 despite 13 spine items | S1.F1: `Stats` definition text; S1.P: spine |
| Completion and dates | Published and Completed dates are present in ISO date form | S1.F1: `Stats` definition text |
| Series | No Series row and no package series metadata are present | S1.F1: tags list; S1.P: metadata |
| Additional descriptive fields | Title, byline, summary, and author's notes are present in the second preface part | S1.F2 |

| Category label in sample-A | Linked tag count |
|---|---:|
| Rating | 1 |
| Archive Warning | 1 |
| Category | 1 |
| Fandoms | 2 |
| Relationships | 4 |
| Characters | 7 |
| Additional Tags | 14 |

Every categorized value has its own AO3 tag link. Display text and URL slugs can differ. The completion information describes the AO3 work represented by this downloaded version; it does not establish the user's reading status. The author's notes are distinct from the user's personal notes. [S1.F1–F2]

## Cross-check against official AO3 source

The inspected source snapshot is commit `aa99cd45c8b39a60944c4e038a53f5a051098b71`; it is not evidence that every historical download used this exact implementation.

- AO3 passes title, authors, summary, language, dates, and a flattened tag list to Calibre. Its conversion publication-date value comes from the work's revision date, rather than its original publication date. It adds a generic subject and passes only the first series and its position as conversion metadata. [S2]
- The download preface renders tags by category, series links and positions, a work URL, and statistics. Its series loop can include multiple series even though conversion metadata takes only the first. [S3]
- Visible tag categories are rating, archive warning, category, fandom, relationship, character, and additional tags. Preface labels are translated. [S3–S4]

## Importer recommendations, not yet validated behavior

- Read both package metadata and preface XHTML. Resolve spine items through the manifest and handle a preface split across documents; do not assume one fixed filename or one preface element.
- Pair category labels with their definition elements and read individual tag anchors. Preserve display text and source links separately instead of reconstructing names from URL slugs or splitting displayed lists on commas.
- Treat the canonical AO3 work link as a work-identity candidate. Keep package UUID, EPUB-copy identity, downloaded-version comparison, and identical-duplicate detection as separate concerns.
- Read the reported chapter pair from work statistics rather than deriving chapter count from spine length. Parse work statistics separately from personal reading status, and keep author's notes separate from personal notes.

## Limits and untested cases

This is one English-language specimen. Other languages, older exporters, converted EPUBs, missing or malformed metadata, incomplete works, and series-bearing works remain untested. No pair of EPUB copies has been compared: identifying identical duplicates versus different downloaded versions has **not** been validated. No conclusions about content equality or later chapter additions follow from the package UUID, word count, or chapter count alone.

## Official source references

- **S2:** [AO3 download metadata and conversion command](https://github.com/otwcode/otwarchive/blob/aa99cd45c8b39a60944c4e038a53f5a051098b71/app/models/download_writer.rb#L67-L174).
- **S3:** [AO3 download preface template](https://github.com/otwcode/otwarchive/blob/aa99cd45c8b39a60944c4e038a53f5a051098b71/app/views/downloads/_download_preface.html.erb#L1-L55).
- **S4:** [AO3 visible tag categories](https://github.com/otwcode/otwarchive/blob/aa99cd45c8b39a60944c4e038a53f5a051098b71/app/models/tag.rb#L10-L23).

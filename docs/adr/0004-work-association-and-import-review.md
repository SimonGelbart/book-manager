---
status: accepted
date: 2026-11-03
---

# Use conservative source evidence for work association

An EPUB copy is associated with an existing Work automatically only when the
copy has a recognized source work identifier, exactly one existing Work has
that identifier, and the available title and author metadata is compatible
with that candidate. A source identifier is evidence for association, not the
catalogue identity itself.

The file reference remains the identity of an already imported copy, so an
unchanged rescan updates that copy. EPUB package identifiers, filenames,
descriptions, and title/author metadata without a recognized source work
identifier are not sufficient evidence for automatic association. An ordinary
EPUB without an AO3 link therefore imports as a new Work and does not enter
Needs review merely because it lacks AO3 metadata.

When a source-enriched import has no exact source-identifier candidate but its
available title and author metadata match an existing source-independent Work,
that Work is shown as an uncertain review candidate. The candidate can help
the owner associate the import, but it never causes automatic association.

When the source identifier has conflicting title or author evidence, or more
than one existing Work is a candidate, keep the imported file and metadata in
a durable Needs review record. Do not create a Work or merge personal data
until the owner explicitly chooses a new Work or one candidate Work. A
resolution adds the copy and metadata to the chosen Work while retaining its
existing reading status, note, rating, Like, and other catalogue information.

This policy favors preserving unrelated works and personal information over
guessing. It can be revisited when additional validated identity evidence is
available; package identifiers alone must not be promoted to Work identity.

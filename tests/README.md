# Automated tests and fixtures

Automated test projects, shared test helpers, and committed synthetic EPUB fixtures belong under this repository-root directory.

The [MVP specification draft](../docs/specs/mvp-epub-catalogue.md#testing-decisions) proposes testing observable behavior primarily through the shared Application interface, with real extraction and temporary-database persistence. Tests can compose Infrastructure adapters without adding an Infrastructure dependency to Application. Target-device checks separately establish native picker, grant, graphics, SQLite, and reader-launch behavior.

The primary testing interface still needs the owner's expectation check, and the .NET testing framework has not been selected. This directory records the required layout; it does not claim that test projects or passing tests already exist.

Commit synthetic fixtures only. The owner's private EPUB and its identifying metadata remain outside public test assets.

# Tome Raider — automated tests and fixtures

Automated test projects, shared test helpers, and committed synthetic EPUB fixtures belong under this repository-root directory.

The owner-approved [MVP specification](../docs/specs/mvp-epub-catalogue.md#testing-decisions) uses the shared Application interface as the primary behavior-test seam, with real extraction and temporary-database persistence. Tests can compose Infrastructure adapters without adding an Infrastructure dependency to Application. Target-device checks separately establish native picker, grant, graphics, SQLite, and reader-launch behavior.

Use **TUnit**, with **Testcontainers when an implemented integration needs containerized dependencies**. Automated architecture tests are required to enforce the Clean Architecture dependency rules. The [testing conventions](../docs/development/testing.md) describe the accepted tooling and required checks.

This directory records the required layout; it does not claim that test projects or passing tests already exist.

Commit synthetic fixtures only. The owner's private EPUB and its identifying metadata remain outside public test assets.

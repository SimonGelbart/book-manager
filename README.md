# Tome Raider

*Raid. Hoard. Read.*

Project discovery and planning are in progress.

The MVP is a personal EPUB catalogue with reading status, notes, ratings, and Likes. It includes basic non-AO3 EPUB support and richer AO3 metadata extraction; [ADR 0002](docs/adr/0002-source-independent-work-identity.md) records the source-independent identity decision.

See the [planning working agreement](docs/planning/working-agreement.md) for our workflow and open questions.

The [product brief](docs/planning/product-brief.md) records the needs discovered so far. The [domain glossary](CONTEXT.md) defines the terms used in the project.

The MVP product scope and testing approach are settled. The [specification](docs/specs/mvp-epub-catalogue.md) collects user stories, acceptance examples, implementation gates, and the accepted Application test seam.

The [approved implementation tickets](docs/planning/implementation-plan.md) link the published GitHub issues, vertical slices, and blocking dependencies. The existing runtime proof is the first task.

The owner chose **Clean Architecture without CQRS**. Production projects belong under [src](src/README.md), and automated tests and fixtures under [tests](tests/README.md); [ADR 0003](docs/adr/0003-clean-architecture-without-cqrs.md) records the dependency and layout rules.

The [testing conventions](docs/development/testing.md) specify TUnit, conditional Testcontainers use, and required architecture checks.

The [agent-skill setup guide](docs/development/agent-skills.md) explains the plugin and optional repository-local installation choices. Project instructions live in `AGENTS.md` and the linked repository documents.

The selected direction is Avalonia with a shared .NET/C# core; [ADR 0001](docs/adr/0001-avalonia-and-shared-dotnet-core.md) records why. Target-platform validation is still pending; see the [capability proof record](docs/research/avalonia-proof-execution.md).

# Tome Raider — production source

Production projects and runtime-proof implementation belong under this repository-root directory.

Follow [Clean Architecture without CQRS](../docs/adr/0003-clean-architecture-without-cqrs.md):

- Domain defines collection and reading concepts and rules independently of the outer layers.
- Application depends on Domain, exposes cohesive use-case interfaces, and owns the contracts its adapters implement.
- Infrastructure implements persistence and EPUB extraction, including optional AO3 enrichment.
- Presentation calls Application through Avalonia views and presentation logic.
- Desktop and Android hosts compose the concrete implementations and platform adapters.

Use ordinary Application methods for lookups and changes through one catalogue design. Use **TomeRaider** as the .NET project and namespace prefix; concrete project names and assembly counts will follow the module responsibilities during scaffolding. The [architecture outline](../docs/architecture/cross-platform-proposal.md) records the dependency direction and platform responsibilities.

---
status: accepted
date: 2026-11-01
---

# Use Clean Architecture without CQRS

The owner explicitly chose Clean Architecture, rejected CQRS, and required repository-root `src/` and `tests/` directories. Use inward code dependencies to keep collection and reading rules independent of Avalonia, persistence, EPUB parsing libraries, and operating-system integrations, while keeping application use cases accessible through ordinary method calls.

Domain has no dependency on the other production layers. Application depends on Domain and owns the interfaces it needs for persistence, extraction, and platform operations. Infrastructure and presentation sit outside Application; platform hosts compose their concrete adapters at startup. Business behavior belongs in the inner layers rather than the host composition code.

Use one cohesive catalogue application design and persistence model for lookups and changes. Do not introduce separate command/query subsystems, command/query handler pipelines, or split read/write stores as a CQRS architecture. Application methods can still accept input models and return results appropriate to a use case.

Put production projects and runtime-proof code under `src/`; put automated test projects, shared test helpers, and committed synthetic fixtures under `tests/`. Concrete project names and assembly counts can follow actual module responsibilities when scaffolding begins. The proposed primary automated test interface remains the Application interface; tests can compose real Infrastructure adapters without adding an Infrastructure dependency to Application.

This extends ADRs 0001 and 0002. The trade-off is explicit dependency and project discipline instead of the smaller initial footprint of a UI-owned implementation. It does not require a handler per operation or a separate abstraction for every class.

The owner subsequently selected TUnit, conditional Testcontainers use, and mandatory architecture tests; the [testing conventions](../development/testing.md) record those choices and the dependency checks that enforce this ADR.

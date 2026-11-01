# Testing conventions

Owner choices confirmed on 2026-11-01: **TUnit** for automated .NET tests, **Testcontainers when an integration needs a containerized dependency**, and **automated architecture tests** to enforce the chosen design.

## Test organization and execution

Keep automated test projects, helpers, and committed synthetic EPUB fixtures under repository-root `tests/`. Production implementation stays under `src/`. Test project names and the final assembly layout will follow the application's name and actual module responsibilities.

Use TUnit for behavior, integration, and architecture test projects. Select compatible stable package versions and runner configuration during scaffolding, using the [toolchain research](../research/dotnet-testing-toolchain.md) as candidate evidence. No package restore, executable TUnit test, or architecture rule run has yet been demonstrated in this workspace.

Tests should assert public outcomes rather than private helpers or internal call sequences. The [specification](../specs/mvp-epub-catalogue.md#testing-decisions) proposes the shared Application interface as the primary behavior-test seam; that proposal still needs the owner's expectation check. Architecture tests are an additional accepted requirement, independent of that pending check.

## Integration tests

Exercise the real extraction and persistence implementations where those behaviors are under test. For the embedded SQLite candidate, use an isolated temporary database file and reopen the catalogue to verify durability. Keep databases and test-owned files isolated between tests, and clean up resources after execution.

Use Testcontainers when an implemented integration depends on a containerized service and realistic isolation is useful. Do not introduce a new application dependency solely to justify a container. Keep container-required test selection explicit and configure the runtime in the environment that runs it. An unavailable required runtime must not produce a misleading passing integration result.

Ordinary embedded-SQLite behavior tests and architecture checks should be runnable without a container runtime. Real platform checks still establish Android SQLite asset loading, document grants, and other host-specific behavior.

## Required architecture checks

Implement automated checks for the dependency rules in [ADR 0003](../adr/0003-clean-architecture-without-cqrs.md). Run them in the standard automated test workflow and CI once test projects are scaffolded.

| Area | Rule to enforce |
|---|---|
| Domain | No references to Application, Infrastructure, Presentation, or platform hosts; no UI/persistence/platform implementation dependencies. |
| Application | Production dependencies point to Domain; required adapter interfaces are owned inward. No references to Infrastructure, Avalonia presentation, or platform-host implementations. |
| Infrastructure | Implements inner interfaces and can depend on Application/Domain; no dependency on Presentation or platform-host composition. |
| Presentation | Uses Application and permitted inner contracts; no dependency on concrete catalogue/extraction implementations or host startup code. |
| Hosts | Explicit composition-root allowances for wiring outer implementations; other production layers must not depend on hosts. |
| Production module graph | Enforce the allowed reference direction and reject forbidden references or cycles between defined modules. |

Check both production project references and emitted code/type dependencies where feasible. A source project must not gain an outward reference merely because its current code does not use it. Conversely, folder or namespace naming alone is not proof that dependencies obey the design.

Maintain explicit, nonempty selections for the production assemblies/modules under test. Fail or report an unmet prerequisite when required inputs are absent; a rule over zero selected types must not masquerade as architecture compliance. Report the violated rule and offending dependency clearly. Demonstrate during initial setup that a deliberately introduced forbidden reference makes the check fail.

Framework-neutral architecture-rule libraries can be called from TUnit. The particular architecture library remains a candidate until compatible package resolution and executable rule checks are verified. Platform-host coverage must be explicit: use appropriate build outputs or project-reference checks rather than assuming Android-specific assemblies can be consumed by a plain desktop test project.

These checks enforce structural dependency rules. Review still evaluates use-case cohesion and the decision to use ordinary Application methods without CQRS; a blanket ban on names such as Query would also reject legitimate lookups.

## Fixtures and platform evidence

Commit synthetic fixtures only. Keep the owner's private EPUB, identifying metadata, and narrative outside public fixtures and test logs. Behavior tests must not need a live AO3 connection.

Use separate target-platform smoke checks for native startup, real file picking and streams, Android retained access after app/device restart, SQLite native loading, and default-reader launch. A passing TUnit suite or container test is not evidence that these target-device workflows have run.

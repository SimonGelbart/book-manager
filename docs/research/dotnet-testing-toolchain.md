# .NET testing toolchain research

Checked **2026-11-01** against official documentation, tagged source, NuGet V3 indexes/registration records, and published package contents. The user selected **TUnit**, **Testcontainers when an integration needs containers**, and architecture tests for Clean Architecture without CQRS, with production code under `src/` and tests under `tests/`. **The architecture library below is a recommendation, not an accepted choice or an executed compatibility proof.**

## Stable package candidates

| Package | Latest stable checked | Published target evidence |
|---|---|---|
| `TUnit` | **1.77.0**, published 2026-10-31 | Actual `net10.0` DLLs and dependency group, also targeting .NET 8/9 and .NET Standard. [P1], [P2] |
| `Testcontainers` | **4.16.0**, published 2026-10-08 | Actual `net10.0` DLL, alongside other targets. This is the framework-neutral container library. [P3], [P4] |
| `TngTech.ArchUnitNET` | **0.13.5**, published 2026-10-26 | Actual `netstandard2.0` and `net462` DLLs; .NET 10 compatibility is inferred from the compatible target, not a dedicated .NET 10 build. Exclude `2.1.0-draft` from stable selection. [P5] |
| `NetArchTest.Rules` | **1.3.2** | Actual `netstandard2.0` DLL; .NET 10 is a compatible consuming target, not an executed test here. [P6] |

The package ZIPs were inspected in memory; nothing was installed or restored. TUnit/Engine 1.77.0 depends on `Microsoft.Testing.Platform` and `Microsoft.Testing.Platform.MSBuild` **>=2.5.2**. ArchUnitNET 0.13.5 depends on Mono.Cecil >=0.11.6 and CycleDetection >=2.0.0; NetArchTest 1.3.2 depends on Mono.Cecil >=0.11.3. These are declared minima, not a resolved lock file. [P2], [P5], [P6]

## TUnit on the .NET 10 SDK

Use an executable `net10.0` test project and pin `TUnit` 1.77.0. TUnit supplies its entry point; remove an independently generated `Program.cs`/`Main`. Its packaged build properties set `OutputType=Exe`, `IsTestProject=true`, and testing-platform application properties. The official manual setup also shows `OutputType=Exe` explicitly. Do not combine this setup with `Microsoft.NET.Test.Sdk` or Coverlet's VSTest collectors; TUnit includes MTP coverage/TRX extensions. **Await TUnit assertions**, since unawaited assertions do not execute. [P2], [T1], [T2]

For the native .NET 10 `dotnet test` experience, merge this runner setting into the repository's SDK-pinning `global.json`:

```json
{
  "test": {
    "runner": "Microsoft.Testing.Platform"
  }
}
```

Without that selection, VSTest remains the default. Native MTP mode requires .NET 10 SDK and MTP >=1.7, and all test projects in the selected run must use MTP. TUnit's declared >=2.5.2 platform dependency satisfies the documented minimum on paper. `dotnet run --project <test-project>` is a separate direct-executable route; selecting the `dotnet test` runner is not a requirement to execute the binary directly. Some TUnit examples describe older forwarding syntax; use Microsoft's .NET 10 MTP CLI reference for this repository's commands. [M1], [M2], [T2]

## Where Testcontainers is needed

Testcontainers requires a **Docker-API-compatible runtime**. Its maintainers actively test Docker on Linux and Docker Desktop on Windows/macOS; alternatives can require configuration and may lack features. It is a development/test dependency, not evidence of a hosted production service requirement. Use the core library from TUnit setup/cleanup when a test genuinely needs an external containerized service; an xUnit/NUnit fixture package is unnecessary. [C1], [P4]

**Recommendation:** run SQLite repository/migration integration tests against the application's real SQLite provider and a unique temporary **file-backed** database. SQLite runs in-process and reads/writes files without a database server, so these tests do not need Testcontainers. Use an explicit temporary filename for close/reopen persistence checks: an empty `Data Source` creates a temporary database deleted on close, while `:memory:` is not a persistent file. Isolate each test's directory/database because TUnit runs tests concurrently by default. This exercises real SQL and persistence without substituting a different database engine. It does not validate Android permissions, native packaging, or device behavior. [S1], [S2], [P1]

## Architecture library comparison

| Concern | ArchUnitNET 0.13.5 | NetArchTest.Rules 1.3.2 |
|---|---|---|
| TUnit without other framework wrappers | Core rules expose `HasNoViolations(architecture)` and `Evaluate(architecture)`; assert their results with TUnit. [A1], [A2] | Framework-neutral `GetResult().IsSuccessful` and failing-type results can be asserted with TUnit. [N1] |
| Layer/type dependencies | Imports compiled architecture and supports assembly/namespace selections plus dependency/member rules. [A1] | Supports type/namespace dependency, naming and design rules using Mono.Cecil. [N1], [P6] |
| Cycles | Documented slice rule `BeFreeOfCycles()`. [A1] | No cycle primitive is documented in the checked stable README/API examples; custom rules are extensible. Do not claim equivalent cycle coverage without an additional check. [N1] |

An official `TngTech.ArchUnitNET.TUnit` **0.13.5** adapter also exists. Its dependency is `TUnit.Assertions >=0.52.51`, **not a hard pin**, but its exception subclasses that assertion library's `BaseAssertionException`. Whether that compiled adapter works with TUnit 1.77.0 remains unexecuted. [A3], [A4]

**Recommendation pending executable proof:** start with **ArchUnitNET core** for its dependency and cycle rules, using TUnit assertions rather than adding a framework wrapper. Retain useful evaluation diagnostics. NetArchTest is a smaller alternative for straightforward dependency rules; its simpler API does not establish the same documented cycle functionality.

Check the **production project-reference graph separately** from emitted type dependencies. MSBuild `ProjectReference` is a project/build relationship and can even omit an assembly reference; bytecode rules alone cannot establish the complete project graph. Fail missing assemblies and empty expected type/layer selections explicitly. Validate which platform assemblies can actually be inspected; do not assume every Android/desktop entry assembly loads in a plain `net10.0` host. [M3], [A1]

No restore, build, test discovery, assertion-failure check, Docker startup, or device run was performed for this research. Before acceptance, prove discovery and a passing test, intentional forbidden-dependency/cycle failures, empty-selection failures, SQLite reopen behavior, and any required container lifecycle with the pinned/resolved packages. ArchUnitNET's published guidance recommends Debug configuration because it analyzes compiled binaries. [P5]

[P1]: https://www.nuget.org/packages/TUnit/1.77.0 "TUnit stable package and parallel execution model"
[P2]: https://api.nuget.org/v3-flatcontainer/tunit.engine/1.77.0/tunit.engine.1.77.0.nupkg "Inspected TUnit Engine nuspec, net10.0 DLL and buildTransitive/net10.0/TUnit.Engine.props"
[P3]: https://www.nuget.org/packages/Testcontainers/4.16.0 "Testcontainers stable package"
[P4]: https://api.nuget.org/v3-flatcontainer/testcontainers/4.16.0/testcontainers.4.16.0.nupkg "Inspected Testcontainers target assets and framework-neutral dependencies"
[P5]: https://www.nuget.org/packages/TngTech.ArchUnitNET/0.13.5 "ArchUnitNET targets, dependencies, version history and Debug recommendation"
[P6]: https://www.nuget.org/packages/NetArchTest.Rules/1.3.2 "NetArchTest targets and dependencies"
[T1]: https://tunit.dev/docs/getting-started/installation/ "Official executable-project setup and incompatible VSTest packages"
[T2]: https://tunit.dev/docs/troubleshooting/ "Awaited assertions, .NET 10 CLI behavior and runner conflicts"
[M1]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test "global.json runner selection and platform minimum"
[M2]: https://learn.microsoft.com/en-us/dotnet/core/tools/dotnet-test-mtp "Version-specific MTP CLI and all-MTP project requirement"
[M3]: https://learn.microsoft.com/en-us/visualstudio/msbuild/common-msbuild-project-items#projectreference "ProjectReference build and assembly-reference semantics"
[C1]: https://dotnet.testcontainers.org/ "Official container runtime requirements and tested environments"
[S1]: https://www.sqlite.org/serverless.html "SQLite's in-process file-access architecture"
[S2]: https://learn.microsoft.com/en-us/dotnet/standard/data/sqlite/connection-strings "File paths, temporary databases and in-memory connection strings"
[A1]: https://archunitnet.readthedocs.io/en/latest/guide/ "Official dependency rules, cycle rules and framework-neutral evaluation"
[A2]: https://github.com/TNG/ArchUnitNET/blob/0.13.5/ArchUnitNET/Fluent/ICanBeEvaluated.cs "Pinned core evaluation interface"
[A3]: https://www.nuget.org/packages/TngTech.ArchUnitNET.TUnit/0.13.5 "Official TUnit adapter and assertion dependency minimum"
[A4]: https://github.com/TNG/ArchUnitNET/blob/0.13.5/ArchUnitNET.TUnit/FailedArchRuleException.cs "Pinned adapter's assertion exception dependency"
[N1]: https://github.com/BenMorris/NetArchTest/blob/v1.3.2/README.md "Stable framework-neutral result API, dependency rules and custom rules"

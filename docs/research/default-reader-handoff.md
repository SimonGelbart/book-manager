# Default reader handoff evidence

Date: 2026-11-06  
Host: Pop!_OS 24.04 LTS, Linux `7.2.6-76070206-generic`, x86_64

## Controlled application evidence

The Application tests use a controlled `IReaderLauncher` and cover:

- opening the selected copy rather than another copy of the same work;
- preserving the reading status and personal note after a successful request;
- reporting an inaccessible file and a failed reader launch without changing catalogue data; and
- reporting a copy that is no longer present in the catalogue.

The presentation test also exercises the visible copy action through `MainViewModel`.

The Application test project built successfully with:

```sh
rtk dotnet build tests/TomeRaider.Application.Tests/TomeRaider.Application.Tests.csproj --configuration Release --no-restore -v minimal
```

The Application test module completed with 48 passing tests using:

```sh
rtk dotnet test --test-modules "tests/TomeRaider.Application.Tests/bin/Release/net10.0/TomeRaider.Application.Tests.dll" --root-directory . --results-directory TestResults
```

The architecture test module also completed with 11 passing tests using:

```sh
rtk dotnet test --test-modules "tests/TomeRaider.Architecture.Tests/bin/Release/net10.0/TomeRaider.Architecture.Tests.dll" --root-directory . --results-directory TestResults
```

## Pop!_OS host handoff check

The host-level association check used a synthetic path only:

```sh
xdg-mime query default application/epub+zip
cp /etc/hosts /tmp/tome-raider-reader-handoff.epub
xdg-open /tmp/tome-raider-reader-handoff.epub
```

`xdg-mime` returned no configured default. `xdg-open` reported routing the EPUB MIME type to File Roller, then the headless session reported `Failed to open display`; no user-visible reader session was established. The temporary file was removed after the check.

This verifies only that the host attempted MIME-based routing for an EPUB path. It is not evidence of a successful EPUB reader launch. A real reader handoff remains unverified until a graphical session with an EPUB-capable default application is available.

No Windows evidence was collected.

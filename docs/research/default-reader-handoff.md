# Default reader handoff evidence

Date: 2026-11-06  
Host: Pop!_OS 24.04, Linux `7.2.6-76070206-generic`, x86_64

## Controlled application evidence

The Application tests use a controlled `IReaderLauncher` and cover:

- opening the selected copy rather than another copy of the same work;
- preserving the reading status and personal note after a successful request;
- reporting an inaccessible file and a failed reader launch without changing catalogue data; and
- reporting a copy that is no longer present in the catalogue.

The presentation test also exercises the visible copy action through `MainViewModel`.

## Host handoff check

The host-level association check was run with:

```sh
xdg-mime query default application/epub+zip
cp /etc/hosts /tmp/tome-raider-reader-handoff.epub
xdg-open /tmp/tome-raider-reader-handoff.epub
```

`xdg-mime` returned no configured default. `xdg-open` routed the existing path to
File Roller and returned successfully, but the process could not open a display in
this headless session (`Failed to open display`). The temporary file was removed.

This is evidence that the Pop!_OS host can route an EPUB path through `xdg-open`,
not evidence of a successful user-visible EPUB reading session. A real reader
launch remains unverified until a desktop session with an EPUB-capable default
application is available.

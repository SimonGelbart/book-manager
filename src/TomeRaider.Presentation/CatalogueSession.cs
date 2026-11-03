using Avalonia.Platform.Storage;
using TomeRaider.Application;

namespace TomeRaider.Presentation;

public sealed record CatalogueSession(CatalogueApplication Application, IFileSource FileSource);

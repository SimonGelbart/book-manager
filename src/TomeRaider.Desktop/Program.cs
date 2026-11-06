using Avalonia;
using Avalonia.Platform.Storage;
using TomeRaider.Application;
using TomeRaider.Infrastructure;
using TomeRaider.Platform.Storage;
using TomeRaider.Presentation;

namespace TomeRaider.Desktop;

internal static class Program
{
    [STAThread]
    public static void Main(string[] args)
    {
        App.ConfigureSessionFactory(CreateSession);
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .WithInterFont()
            .LogToTrace();
    }

    private static CatalogueSession CreateSession(IStorageProvider storageProvider)
    {
        var dataDirectory = GetDataDirectory();
        return new CatalogueSession(
            new CatalogueApplication(
                new EpubMetadataExtractor(),
                new SqliteCatalogue(Path.Combine(dataDirectory, "catalogue.sqlite")),
                new DesktopReaderLauncher()),
            new AvaloniaStorageFileSource(
                storageProvider,
                new JsonFolderBookmarkStore(Path.Combine(dataDirectory, "folder-bookmark.json"))));
    }

    private static string GetDataDirectory()
    {
        var directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "TomeRaider");
        Directory.CreateDirectory(directory);
        return directory;
    }
}

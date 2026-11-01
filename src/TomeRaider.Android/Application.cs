using Android.App;
using Android.Runtime;
using Avalonia;
using Avalonia.Android;
using Avalonia.Platform.Storage;
using TomeRaider.Application;
using TomeRaider.Infrastructure;
using TomeRaider.Platform.Storage;
using TomeRaider.Presentation;

namespace TomeRaider.Android;

[Application]
public class MainApplication : AvaloniaAndroidApplication<App>
{
    protected MainApplication(nint javaReference, JniHandleOwnership transfer)
        : base(javaReference, transfer)
    {
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        App.ConfigureSessionFactory(CreateSession);
        return base.CustomizeAppBuilder(builder).WithInterFont();
    }

    private static CatalogueSession CreateSession(IStorageProvider storageProvider)
    {
        var dataDirectory = GetDataDirectory();
        return new CatalogueSession(
            new CatalogueApplication(
                new EpubMetadataExtractor(),
                new SqliteCatalogue(Path.Combine(dataDirectory, "catalogue.sqlite"))),
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

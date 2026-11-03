using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

namespace TomeRaider.Presentation;

public partial class App : Avalonia.Application
{
    private static Func<IStorageProvider, CatalogueSession>? configuredSessionFactory;
    private readonly Func<IStorageProvider, CatalogueSession> sessionFactory;

    public App()
    {
        sessionFactory = configuredSessionFactory
            ?? throw new InvalidOperationException("The host must configure the proof composition before starting Avalonia.");
    }

    public static void ConfigureSessionFactory(Func<IStorageProvider, CatalogueSession> factory)
    {
        configuredSessionFactory = factory;
    }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new MainWindow(sessionFactory);
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MainView(sessionFactory);
        }

        base.OnFrameworkInitializationCompleted();
    }
}

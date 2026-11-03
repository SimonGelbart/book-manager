using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace TomeRaider.Presentation;

public partial class MainWindow : Window
{
    public MainWindow()
        : this(_ => throw new InvalidOperationException("The host composition is not available in the designer."))
    {
    }

    public MainWindow(Func<IStorageProvider, CatalogueSession> sessionFactory)
    {
        InitializeComponent();
        Content = new MainView(sessionFactory);
    }
}

using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;

namespace TomeRaider.Presentation;

public partial class MainView : UserControl
{
    private readonly Func<IStorageProvider, CatalogueSession> sessionFactory;

    public MainView()
        : this(_ => throw new InvalidOperationException("The host composition is not available in the designer."))
    {
    }

    public MainView(Func<IStorageProvider, CatalogueSession> sessionFactory)
    {
        this.sessionFactory = sessionFactory;
        InitializeComponent();
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null && DataContext is null)
        {
            DataContext = new MainViewModel(sessionFactory(topLevel.StorageProvider));
        }
    }

    private async void SelectFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.SelectFolderAndImportAsync();
        }
    }

    private async void ImportSavedFolder(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.ImportSavedFolderAsync();
        }
    }

    private async void SaveReading(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.SaveReadingAsync();
        }
    }
}

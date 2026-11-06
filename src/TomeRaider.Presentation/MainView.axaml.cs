using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace TomeRaider.Presentation;

public partial class MainView : UserControl
{
    private readonly Func<IStorageProvider, CatalogueSession> sessionFactory;
    private MainViewModel? observedViewModel;
    private Guid? programmaticSelectionIdToIgnore;

    public MainView()
        : this(_ => throw new InvalidOperationException("The host composition is not available in the designer."))
    {
    }

    public MainView(Func<IStorageProvider, CatalogueSession> sessionFactory)
    {
        this.sessionFactory = sessionFactory;
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        AttachedToVisualTree += OnAttachedToVisualTree;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (observedViewModel is not null)
        {
            observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        observedViewModel = DataContext as MainViewModel;
        if (observedViewModel is not null)
        {
            observedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            SyncSelectedWork(observedViewModel);
        }

        programmaticSelectionIdToIgnore = null;
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MainViewModel.SelectedWork)
            && sender is MainViewModel viewModel)
        {
            SyncSelectedWork(viewModel);
        }
    }

    private void SyncSelectedWork(MainViewModel viewModel)
    {
        if (!WorksList.CheckAccess())
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (ReferenceEquals(DataContext, viewModel))
                {
                    SyncSelectedWork(viewModel);
                }
            });
            return;
        }

        programmaticSelectionIdToIgnore = viewModel.SelectedWork?.Id;
        WorksList.SetCurrentValue(ListBox.SelectedItemProperty, viewModel.SelectedWork);
    }

    private async void OnAttachedToVisualTree(object? sender, VisualTreeAttachmentEventArgs e)
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is not null && DataContext is null)
        {
            var viewModel = new MainViewModel(sessionFactory(topLevel.StorageProvider));
            DataContext = viewModel;
            await viewModel.InitializeAsync();
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

    private async void ResolveReviewAsNew(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Button { DataContext: ImportReviewViewModel review })
        {
            await viewModel.ResolveImportReviewAsNewAsync(review.Id);
        }
    }

    private async void ResolveReviewToExisting(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel
            && sender is Button { Tag: ImportReviewCandidateViewModel candidate })
        {
            await viewModel.ResolveImportReviewToExistingAsync(candidate.ReviewId, candidate.WorkId);
        }
    }

    private void RequestClearCollection(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.RequestClearCollection();
        }
    }

    private async void ConfirmClearCollection(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            await viewModel.ConfirmClearCollectionAsync();
        }
    }

    private void CancelClearCollection(object? sender, RoutedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel)
        {
            viewModel.CancelClearCollection();
        }
    }

    private async void SelectWork(object? sender, SelectionChangedEventArgs e)
    {
        if (DataContext is MainViewModel viewModel
            && sender is ListBox { SelectedItem: WorkSummaryViewModel work })
        {
            if (programmaticSelectionIdToIgnore == work.Id)
            {
                programmaticSelectionIdToIgnore = null;
                return;
            }

            programmaticSelectionIdToIgnore = null;
            await viewModel.SelectWorkAsync(work.Id);
        }
    }
}

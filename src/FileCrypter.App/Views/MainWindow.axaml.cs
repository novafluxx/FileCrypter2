using System.ComponentModel;
using Avalonia.Controls;
using FileCrypter.App.ViewModels;

namespace FileCrypter.App.Views;

public partial class MainWindow : Window
{
    private const double ExpandedSidebarWidth = 248;
    private const double CollapsedSidebarWidth = 72;

    private MainWindowViewModel? subscribedViewModel;

    public MainWindow()
    {
        InitializeComponent();
    }

    protected override void OnDataContextChanged(EventArgs e)
    {
        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }

        subscribedViewModel = DataContext as MainWindowViewModel;

        if (subscribedViewModel is not null)
        {
            subscribedViewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplySidebarState(subscribedViewModel.IsSidebarCollapsed);
        }

        base.OnDataContextChanged(e);
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(MainWindowViewModel.IsSidebarCollapsed) &&
            sender is MainWindowViewModel viewModel)
        {
            ApplySidebarState(viewModel.IsSidebarCollapsed);
        }
    }

    private void ApplySidebarState(bool isCollapsed)
    {
        double width = isCollapsed ? CollapsedSidebarWidth : ExpandedSidebarWidth;
        RootLayout.ColumnDefinitions[0].Width = new GridLength(width);
    }
}

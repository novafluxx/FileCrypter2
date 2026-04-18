using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;

namespace FileCrypter.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly EncryptViewModel encryptViewModel;
    private readonly DecryptViewModel decryptViewModel;
    private readonly PlaceholderPageViewModel batchViewModel = new(
        "Batch",
        "Batch encryption, batch decryption, and archive workflows will live here.");
    private readonly PlaceholderPageViewModel helpViewModel = new(
        "Help",
        "Keep passwords and key files. FileCrypter cannot recover forgotten passwords or lost or changed key files.");
    private readonly PlaceholderPageViewModel settingsViewModel = new(
        "Settings",
        "Compression defaults and appearance settings will live here.");

    [ObservableProperty]
    private ViewModelBase currentPage;

    [ObservableProperty]
    private string currentPageTitle;

    public MainWindowViewModel()
        : this(new FileCrypterWorkflowService())
    {
    }

    public MainWindowViewModel(
        IFileCrypterWorkflowService workflowService,
        IFilePickerService? filePickerService = null)
    {
        encryptViewModel = new EncryptViewModel(workflowService, filePickerService);
        decryptViewModel = new DecryptViewModel(workflowService, filePickerService);
        currentPage = encryptViewModel;
        currentPageTitle = encryptViewModel.Title;

        PrimaryNavigationItems =
        [
            new NavigationItemViewModel("encrypt", "Encrypt", SelectNavigationItem) { IsSelected = true },
            new NavigationItemViewModel("decrypt", "Decrypt", SelectNavigationItem),
            new NavigationItemViewModel("batch", "Batch", SelectNavigationItem),
        ];
        SecondaryNavigationItems =
        [
            new NavigationItemViewModel("help", "Help", SelectNavigationItem),
            new NavigationItemViewModel("settings", "Settings", SelectNavigationItem),
        ];

        SubscribeToWorkflowStatus(encryptViewModel);
        SubscribeToWorkflowStatus(decryptViewModel);
    }

    public ObservableCollection<NavigationItemViewModel> PrimaryNavigationItems { get; }

    public ObservableCollection<NavigationItemViewModel> SecondaryNavigationItems { get; }

    public string AppVersion => "v0.1.0";

    public string StatusText => CurrentPage is IWorkflowStatusViewModel workflowPage
        ? workflowPage.StatusText
        : "Ready";

    public string FooterDetail => CurrentPage is IWorkflowStatusViewModel workflowPage
        ? workflowPage.ProgressText
        : string.Empty;

    partial void OnCurrentPageChanged(ViewModelBase value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(FooterDetail));
    }

    [RelayCommand]
    private void SelectNavigationItem(NavigationItemViewModel item)
    {
        foreach (NavigationItemViewModel navigationItem in PrimaryNavigationItems.Concat(SecondaryNavigationItems))
        {
            navigationItem.IsSelected = navigationItem == item;
        }

        CurrentPage = item.Key switch
        {
            "encrypt" => encryptViewModel,
            "decrypt" => decryptViewModel,
            "batch" => batchViewModel,
            "help" => helpViewModel,
            "settings" => settingsViewModel,
            _ => encryptViewModel,
        };
        CurrentPageTitle = item.Key == "encrypt"
            ? encryptViewModel.Title
            : item.Key == "decrypt"
                ? decryptViewModel.Title
            : item.Title;
    }

    private void SubscribeToWorkflowStatus(ViewModelBase workflowPage)
    {
        workflowPage.PropertyChanged += OnWorkflowPagePropertyChanged;
    }

    private void OnWorkflowPagePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (!ReferenceEquals(sender, CurrentPage))
        {
            return;
        }

        if (args.PropertyName is nameof(EncryptViewModel.StatusText) or nameof(DecryptViewModel.StatusText))
        {
            OnPropertyChanged(nameof(StatusText));
        }

        if (args.PropertyName is nameof(EncryptViewModel.ProgressText) or nameof(DecryptViewModel.ProgressText))
        {
            OnPropertyChanged(nameof(FooterDetail));
        }
    }
}

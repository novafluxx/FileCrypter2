using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly EncryptViewModel encryptViewModel;
    private readonly DecryptViewModel decryptViewModel;
    private readonly BatchViewModel batchViewModel;
    private readonly SettingsViewModel settingsViewModel;
    private readonly PlaceholderPageViewModel helpViewModel = new(
        "Help",
        "Keep passwords and key files. FileCrypter cannot recover forgotten passwords or lost or changed key files.");

    [ObservableProperty]
    private ViewModelBase currentPage;

    [ObservableProperty]
    private string currentPageTitle;

    public MainWindowViewModel()
        : this(new FileCrypterWorkflowService(), new FileCrypterSettingsService())
    {
    }

    public MainWindowViewModel(
        IFileCrypterWorkflowService workflowService,
        IFileCrypterSettingsService? settingsService = null,
        IFilePickerService? filePickerService = null)
    {
        settingsService ??= new FileCrypterSettingsService();
        FileCrypterSettings initialSettings = LoadInitialSettings(settingsService, out string settingsErrorMessage);

        encryptViewModel = new EncryptViewModel(
            workflowService,
            initialSettings.EnableCompressionByDefault,
            filePickerService);
        decryptViewModel = new DecryptViewModel(workflowService, filePickerService);
        batchViewModel = new BatchViewModel(workflowService, filePickerService);
        settingsViewModel = new SettingsViewModel(settingsService, initialSettings, settingsErrorMessage);
        settingsViewModel.SettingsSaved += ApplySettings;
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
        SubscribeToWorkflowStatus(batchViewModel);
        SubscribeToWorkflowStatus(settingsViewModel);
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
            : item.Key == "batch"
                ? batchViewModel.Title
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

        if (args.PropertyName == nameof(IWorkflowStatusViewModel.StatusText))
        {
            OnPropertyChanged(nameof(StatusText));
        }

        if (args.PropertyName == nameof(IWorkflowStatusViewModel.ProgressText))
        {
            OnPropertyChanged(nameof(FooterDetail));
        }
    }

    private static FileCrypterSettings LoadInitialSettings(
        IFileCrypterSettingsService settingsService,
        out string settingsErrorMessage)
    {
        try
        {
            settingsErrorMessage = string.Empty;
            return settingsService.LoadAsync(CancellationToken.None).GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            settingsErrorMessage = $"Settings error: {exception.Message}";
            return new FileCrypterSettings();
        }
    }

    private void ApplySettings(FileCrypterSettings settings)
    {
        encryptViewModel.ApplySettings(settings);
    }
}

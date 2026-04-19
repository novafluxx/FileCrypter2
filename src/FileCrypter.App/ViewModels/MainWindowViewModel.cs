using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAppMetadataService appMetadataService;
    private readonly IAppThemeService appThemeService;
    private readonly EncryptViewModel encryptViewModel;
    private readonly DecryptViewModel decryptViewModel;
    private readonly BatchViewModel batchViewModel;
    private readonly HelpViewModel helpViewModel;
    private readonly SettingsViewModel settingsViewModel;

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
        IFilePickerService? filePickerService = null,
        IAppMetadataService? appMetadataService = null,
        IAppUpdateService? appUpdateService = null,
        IAppThemeService? appThemeService = null)
    {
        this.appMetadataService = appMetadataService ?? new AppMetadataService();
        this.appThemeService = appThemeService
            ?? (Avalonia.Application.Current is null
                ? new NoOpAppThemeService()
                : new AvaloniaAppThemeService());
        appUpdateService ??= new DevelopmentAppUpdateService();
        settingsService ??= new FileCrypterSettingsService();
        FileCrypterSettings initialSettings = LoadInitialSettings(settingsService, out string settingsErrorMessage);

        encryptViewModel = new EncryptViewModel(
            workflowService,
            initialSettings,
            filePickerService);
        decryptViewModel = new DecryptViewModel(workflowService, initialSettings, filePickerService);
        batchViewModel = new BatchViewModel(workflowService, initialSettings, filePickerService);
        helpViewModel = new HelpViewModel(this.appMetadataService, appUpdateService);
        settingsViewModel = new SettingsViewModel(
            settingsService,
            initialSettings,
            this.appThemeService,
            filePickerService,
            settingsErrorMessage);
        settingsViewModel.SettingsSaved += ApplySettings;
        ApplySettings(initialSettings);
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
        SubscribeToWorkflowStatus(helpViewModel);
        SubscribeToWorkflowStatus(settingsViewModel);
    }

    public ObservableCollection<NavigationItemViewModel> PrimaryNavigationItems { get; }

    public ObservableCollection<NavigationItemViewModel> SecondaryNavigationItems { get; }

    public string AppVersion => appMetadataService.DisplayVersion;

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
            : item.Key == "help"
                ? helpViewModel.Title
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
        appThemeService.ApplyTheme(settings.ThemePreference);
        encryptViewModel.ApplySettings(settings);
        decryptViewModel.ApplySettings(settings);
        batchViewModel.ApplySettings(settings);
    }
}

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
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
        IAppThemeService? appThemeService = null,
        IClipboardService? clipboardService = null,
        IPathRevealService? pathRevealService = null)
    {
        this.appMetadataService = appMetadataService ?? new AppMetadataService();
        this.appThemeService = appThemeService
            ?? (Avalonia.Application.Current is null
                ? new NoOpAppThemeService()
                : new AvaloniaAppThemeService());
        appUpdateService ??= new DevelopmentAppUpdateService();
        settingsService ??= new FileCrypterSettingsService();
        pathRevealService ??= new NoOpPathRevealService();
        FileCrypterSettings initialSettings = LoadInitialSettings(settingsService, out string settingsErrorMessage);

        encryptViewModel = new EncryptViewModel(
            workflowService,
            initialSettings,
            filePickerService,
            clipboardService,
            pathRevealService);
        decryptViewModel = new DecryptViewModel(
            workflowService,
            initialSettings,
            filePickerService,
            clipboardService,
            pathRevealService);
        batchViewModel = new BatchViewModel(
            workflowService,
            initialSettings,
            filePickerService,
            clipboardService,
            pathRevealService);
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
            new NavigationItemViewModel("encrypt", "Encrypt", ClosedLockIconPathData, SelectNavigationItem) { IsSelected = true },
            new NavigationItemViewModel("decrypt", "Decrypt", OpenLockIconPathData, SelectNavigationItem),
            new NavigationItemViewModel("batch", "Batch", BatchDocumentsIconPathData, SelectNavigationItem),
        ];
        SecondaryNavigationItems =
        [
            new NavigationItemViewModel("help", "Help", HelpIconPathData, SelectNavigationItem),
            new NavigationItemViewModel("settings", "Settings", SettingsIconPathData, SelectNavigationItem),
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

    public string FooterActionText => CurrentPage is IWorkflowStatusViewModel workflowPage
        ? workflowPage.FooterActionText
        : string.Empty;

    public ICommand? FooterActionCommand => CurrentPage is IWorkflowStatusViewModel workflowPage
        ? workflowPage.FooterActionCommand
        : null;

    public bool HasFooterAction => CurrentPage is IWorkflowStatusViewModel workflowPage && workflowPage.HasFooterAction;

    partial void OnCurrentPageChanged(ViewModelBase value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(FooterDetail));
        OnPropertyChanged(nameof(FooterActionText));
        OnPropertyChanged(nameof(FooterActionCommand));
        OnPropertyChanged(nameof(HasFooterAction));
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

        if (args.PropertyName is nameof(IWorkflowStatusViewModel.FooterActionText) or
            nameof(IWorkflowStatusViewModel.FooterActionCommand) or
            nameof(IWorkflowStatusViewModel.HasFooterAction))
        {
            OnPropertyChanged(nameof(FooterActionText));
            OnPropertyChanged(nameof(FooterActionCommand));
            OnPropertyChanged(nameof(HasFooterAction));
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

    private const string ClosedLockIconPathData =
        // Shackle (closed arch)
        "M8.5,10 V7.5 C8.5,5.57 10.07,4 12,4 C13.93,4 15.5,5.57 15.5,7.5 V10 " +
        // Body
        "M7,10 H17 C17.55,10 18,10.45 18,11 V18 C18,18.55 17.55,19 17,19 H7 C6.45,19 6,18.55 6,18 V11 C6,10.45 6.45,10 7,10 Z " +
        // Keyhole: small circle + short tail
        "M12,13.2 A0.8,0.8 0 1 1 12,14.8 A0.8,0.8 0 1 1 12,13.2 Z " +
        "M12,14.8 V16.5";

    private const string OpenLockIconPathData =
        // Shackle (open on the left)
        "M15.5,10 V7.5 C15.5,6.12 14.38,5 13,5 C11.62,5 10.5,6.12 10.5,7.5 " +
        // Body (same as closed)
        "M7,10 H17 C17.55,10 18,10.45 18,11 V18 C18,18.55 17.55,19 17,19 H7 C6.45,19 6,18.55 6,18 V11 C6,10.45 6.45,10 7,10 Z " +
        // Keyhole
        "M12,13.2 A0.8,0.8 0 1 1 12,14.8 A0.8,0.8 0 1 1 12,13.2 Z " +
        "M12,14.8 V16.5";

    private const string BatchDocumentsIconPathData =
        // Front document
        "M6,8 H15 C15.55,8 16,8.45 16,9 V19 C16,19.55 15.55,20 15,20 " +
        "H6 C5.45,20 5,19.55 5,19 V9 C5,8.45 5.45,8 6,8 Z " +
        // Back document (visible peek only)
        "M8,8 V5 C8,4.45 8.45,4 9,4 H18 C18.55,4 19,4.45 19,5 V15 " +
        "C19,15.55 18.55,16 18,16 H16 " +
        // Content lines
        "M8,14 H13 M8,17 H11";

    private const string HelpIconPathData =
        // Outer circle
        "M12,4 A8,8 0 1 1 12,20 A8,8 0 1 1 12,4 Z " +
        // Question mark — recentered so stem is at x=12
        "M10,9.5 C10,8.12 11.12,7 12.5,7 C13.88,7 15,8.12 15,9.5 " +
        "C15,10.7 14.38,11.31 13.43,11.93 C12.68,12.41 12,12.84 12,14 " +
        // Dot
        "M12,16.8 V17.2";

    private const string SettingsIconPathData =
        // 6-tooth gear outline — traced clockwise from top tooth
        "M9.67,3.31 L14.33,3.31 L13.81,5.24 L16.95,7.05 L18.36,5.64 L20.69,9.67 " +
        "L18.76,10.19 L18.76,13.81 L20.69,14.33 L18.36,18.36 L16.95,16.95 L13.81,18.76 " +
        "L14.33,20.69 L9.67,20.69 L10.19,18.76 L7.05,16.95 L5.64,18.36 L3.31,14.33 " +
        "L5.24,13.81 L5.24,10.19 L3.31,9.67 L5.64,5.64 L7.05,7.05 L10.19,5.24 Z " +
        // Axle hole
        "M10,12 A2,2 0 1 1 14,12 A2,2 0 1 1 10,12 Z";
}

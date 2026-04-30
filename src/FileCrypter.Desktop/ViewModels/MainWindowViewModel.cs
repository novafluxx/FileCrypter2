using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.Desktop.Services;
using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private static readonly TimeSpan DefaultToastDuration = TimeSpan.FromSeconds(5);

    private readonly IAppMetadataService appMetadataService;
    private readonly IAppThemeService appThemeService;
    private readonly TimeSpan toastDuration;
    private readonly EncryptViewModel encryptViewModel;
    private readonly DecryptViewModel decryptViewModel;
    private readonly BatchViewModel batchViewModel;
    private readonly HelpViewModel helpViewModel;
    private readonly SettingsViewModel settingsViewModel;

    [ObservableProperty]
    private ViewModelBase currentPage;

    [ObservableProperty]
    private bool isSidebarCollapsed;

    [ObservableProperty]
    private WorkflowToastNotification? currentToast;

    private CancellationTokenSource? toastDismissalCancellation;

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
        IPathRevealService? pathRevealService = null,
        TimeSpan? toastDuration = null)
    {
        this.appMetadataService = appMetadataService ?? new AppMetadataService();
        this.appThemeService = appThemeService
            ?? (Avalonia.Application.Current is null
                ? new NoOpAppThemeService()
                : new AvaloniaAppThemeService());
        this.toastDuration = toastDuration ?? DefaultToastDuration;
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
            settingsErrorMessage,
            appMetadataService: this.appMetadataService);
        settingsViewModel.SettingsSaved += ApplySettings;
        ApplySettings(initialSettings);
        currentPage = encryptViewModel;

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
        SubscribeToWorkflowToast(encryptViewModel);
        SubscribeToWorkflowToast(decryptViewModel);
        SubscribeToWorkflowToast(batchViewModel);
    }

    public ObservableCollection<NavigationItemViewModel> PrimaryNavigationItems { get; }

    public ObservableCollection<NavigationItemViewModel> SecondaryNavigationItems { get; }

    public bool IsSidebarExpanded => !IsSidebarCollapsed;

    public string SidebarToggleToolTip => IsSidebarCollapsed ? "Expand sidebar" : "Collapse sidebar";

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

    public bool IsToastVisible => CurrentToast is not null;

    public bool IsToastWarning => CurrentToast?.Kind == WorkflowToastKind.Warning;

    public bool IsToastSuccess => CurrentToast?.Kind == WorkflowToastKind.Success;

    public bool HasToastDetail => !string.IsNullOrWhiteSpace(CurrentToast?.Detail);

    public string ToastTitle => CurrentToast?.Title ?? string.Empty;

    public string ToastMessage => CurrentToast?.Message ?? string.Empty;

    public string ToastDetail => CurrentToast?.Detail ?? string.Empty;

    partial void OnCurrentPageChanged(ViewModelBase value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(FooterDetail));
        OnPropertyChanged(nameof(FooterActionText));
        OnPropertyChanged(nameof(FooterActionCommand));
        OnPropertyChanged(nameof(HasFooterAction));
    }

    partial void OnIsSidebarCollapsedChanged(bool value)
    {
        OnPropertyChanged(nameof(IsSidebarExpanded));
        OnPropertyChanged(nameof(SidebarToggleToolTip));
    }

    partial void OnCurrentToastChanged(WorkflowToastNotification? value)
    {
        OnPropertyChanged(nameof(IsToastVisible));
        OnPropertyChanged(nameof(IsToastWarning));
        OnPropertyChanged(nameof(IsToastSuccess));
        OnPropertyChanged(nameof(HasToastDetail));
        OnPropertyChanged(nameof(ToastTitle));
        OnPropertyChanged(nameof(ToastMessage));
        OnPropertyChanged(nameof(ToastDetail));
    }

    [RelayCommand]
    private void ToggleSidebar()
    {
        IsSidebarCollapsed = !IsSidebarCollapsed;
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
    }

    [RelayCommand]
    private void DismissToast()
    {
        toastDismissalCancellation?.Cancel();
        CurrentToast = null;
    }

    private void SubscribeToWorkflowStatus(ViewModelBase workflowPage)
    {
        workflowPage.PropertyChanged += OnWorkflowPagePropertyChanged;
    }

    private void SubscribeToWorkflowToast(IWorkflowToastSource workflowPage)
    {
        workflowPage.ToastNotificationRequested += OnWorkflowToastNotificationRequested;
    }

    private void OnWorkflowToastNotificationRequested(object? sender, WorkflowToastNotification notification)
    {
        ShowToast(notification);
    }

    private void ShowToast(WorkflowToastNotification notification)
    {
        toastDismissalCancellation?.Cancel();
        toastDismissalCancellation = new CancellationTokenSource();
        CurrentToast = notification;

        if (toastDuration > TimeSpan.Zero)
        {
            _ = AutoDismissToastAsync(notification, toastDismissalCancellation.Token);
        }
    }

    private async Task AutoDismissToastAsync(
        WorkflowToastNotification notification,
        CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(toastDuration, cancellationToken).ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (ReferenceEquals(CurrentToast, notification))
        {
            CurrentToast = null;
        }
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
            settingsErrorMessage = WorkflowErrorMessageFormatter.GetSettingsFailureMessage(exception);
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

    // Sidebar icon path data may include icons adapted from Fluent UI System Icons.
    // See THIRD-PARTY-NOTICES.md for license details.
    private const string ClosedLockIconPathData =
        "M12 1C14.7614 1 17 3.23858 17 6V8.00977C18.6781 8.13743 20 9.5392 20 11.25V18.75C20 20.5449 18.5449 22 16.75 22H7.25C5.45507 22 4 20.5449 4 18.75V11.25C4 9.5392 5.3219 8.13743 7 8.00977V6C7 3.23858 9.23858 1 12 1ZM12 13.75C11.3096 13.75 10.75 14.3096 10.75 15C10.75 15.6904 11.3096 16.25 12 16.25C12.6904 16.25 13.25 15.6904 13.25 15C13.25 14.3096 12.6904 13.75 12 13.75ZM12 2.5C10.067 2.5 8.5 4.067 8.5 6V8H15.5V6C15.5 4.067 13.933 2.5 12 2.5Z";

    private const string OpenLockIconPathData =
        "M18.75 1C21.3734 1 23.5 3.12665 23.5 5.75V6.25C23.5 6.66421 23.1642 7 22.75 7C22.3358 7 22 6.66421 22 6.25V5.75C22 3.95507 20.5449 2.5 18.75 2.5C16.9551 2.5 15.5 3.95507 15.5 5.75V8H16.75C18.5449 8 20 9.45507 20 11.25V18.75C20 20.5449 18.5449 22 16.75 22H7.25C5.45507 22 4 20.5449 4 18.75V11.25C4 9.45507 5.45507 8 7.25 8H14V5.75C14 3.12665 16.1266 1 18.75 1ZM12 13.75C11.3096 13.75 10.75 14.3096 10.75 15C10.75 15.6904 11.3096 16.25 12 16.25C12.6904 16.25 13.25 15.6904 13.25 15C13.25 14.3096 12.6904 13.75 12 13.75Z";

    private const string BatchDocumentsIconPathData =
        "M4 4.25C4 3.00736 5.00736 2 6.25 2H10.5039V6.74753C10.5039 7.99017 11.5113 8.99753 12.7539 8.99753H17.5V17.25C17.5 18.4926 16.4926 19.5 15.25 19.5H6.25C5.00736 19.5 4 18.4926 4 17.25V4.25Z " +
        "M12.0039 6.74753V2.46728C12.0803 2.52611 12.1532 2.59009 12.2221 2.65901L16.841 7.27786C16.9103 7.34721 16.9747 7.42062 17.0338 7.49753H12.7539C12.3397 7.49753 12.0039 7.16174 12.0039 6.74753Z " +
        "M8.75088 22C7.77121 22 6.93778 21.3739 6.62891 20.5H15.2509C17.0458 20.5 18.5009 19.0449 18.5009 17.25V8.93689L19.3419 9.77788C19.7638 10.1998 20.0009 10.7721 20.0009 11.3689V17.25C20.0009 19.8734 17.8742 22 15.2509 22H8.75088Z";

    private const string HelpIconPathData =
        "M12 2C17.523 2 22 6.478 22 12C22 17.522 17.523 22 12 22C6.477 22 2 17.522 2 12C2 6.478 6.477 2 12 2ZM12 15.5C11.4477 15.5 11 15.9477 11 16.5C11 17.0523 11.4477 17.5 12 17.5C12.5523 17.5 13 17.0523 13 16.5C13 15.9477 12.5523 15.5 12 15.5ZM12 6.75C10.4812 6.75 9.25 7.98122 9.25 9.5C9.25 9.91421 9.58579 10.25 10 10.25C10.3797 10.25 10.6935 9.96785 10.7432 9.60177L10.75 9.5C10.75 8.80964 11.3096 8.25 12 8.25C12.6904 8.25 13.25 8.80964 13.25 9.5C13.25 10.0388 13.115 10.3053 12.6051 10.8322L12.4697 10.9697C11.5916 11.8478 11.25 12.4171 11.25 13.5C11.25 13.9142 11.5858 14.25 12 14.25C12.4142 14.25 12.75 13.9142 12.75 13.5C12.75 12.9612 12.885 12.6947 13.3949 12.1678L13.5303 12.0303C14.4084 11.1522 14.75 10.5829 14.75 9.5C14.75 7.98122 13.5188 6.75 12 6.75Z";

    private const string SettingsIconPathData =
        "M12.0122 2.25C12.7462 2.25846 13.4773 2.34326 14.1937 2.50304C14.5064 2.57279 14.7403 2.83351 14.7758 3.15196L14.946 4.67881C15.0231 5.37986 15.615 5.91084 16.3206 5.91158C16.5103 5.91188 16.6979 5.87238 16.8732 5.79483L18.2738 5.17956C18.5651 5.05159 18.9055 5.12136 19.1229 5.35362C20.1351 6.43464 20.8889 7.73115 21.3277 9.14558C21.4223 9.45058 21.3134 9.78203 21.0564 9.9715L19.8149 10.8866C19.4607 11.1468 19.2516 11.56 19.2516 11.9995C19.2516 12.4389 19.4607 12.8521 19.8157 13.1129L21.0582 14.0283C21.3153 14.2177 21.4243 14.5492 21.3297 14.8543C20.8911 16.2685 20.1377 17.5649 19.1261 18.6461C18.9089 18.8783 18.5688 18.9483 18.2775 18.8206L16.8712 18.2045C16.4688 18.0284 16.0068 18.0542 15.6265 18.274C15.2463 18.4937 14.9933 18.8812 14.945 19.3177L14.7759 20.8444C14.741 21.1592 14.5122 21.4182 14.204 21.4915C12.7556 21.8361 11.2465 21.8361 9.79803 21.4915C9.48991 21.4182 9.26105 21.1592 9.22618 20.8444L9.05736 19.32C9.00777 18.8843 8.75434 18.498 8.37442 18.279C7.99451 18.06 7.5332 18.0343 7.1322 18.2094L5.72557 18.8256C5.43422 18.9533 5.09403 18.8833 4.87678 18.6509C3.86462 17.5685 3.11119 16.2705 2.6732 14.8548C2.57886 14.5499 2.68786 14.2186 2.94485 14.0293L4.18818 13.1133C4.54232 12.8531 4.75147 12.4399 4.75147 12.0005C4.75147 11.561 4.54232 11.1478 4.18771 10.8873L2.94516 9.97285C2.6878 9.78345 2.5787 9.45178 2.67337 9.14658C3.11212 7.73215 3.86594 6.43564 4.87813 5.35462C5.09559 5.12236 5.43594 5.05259 5.72724 5.18056L7.12762 5.79572C7.53056 5.97256 7.9938 5.94585 8.37577 5.72269C8.75609 5.50209 9.00929 5.11422 9.05817 4.67764L9.22824 3.15196C9.26376 2.83335 9.49786 2.57254 9.8108 2.50294C10.5281 2.34342 11.26 2.25865 12.0122 2.25ZM11.9997 8.99995C10.3428 8.99995 8.9997 10.3431 8.9997 12C8.9997 13.6568 10.3428 15 11.9997 15C13.6565 15 14.9997 13.6568 14.9997 12C14.9997 10.3431 13.6565 8.99995 11.9997 8.99995Z";
}

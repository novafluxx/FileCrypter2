using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.Desktop.Services;
using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IWorkflowStatusViewModel
{
    private const int DefaultOutputDirectoryAutoSaveDelayMilliseconds = 500;
    private const string IdleAutosaveMessage = "Settings save automatically as you change them.";
    private const string DefaultChangelogVersion = "this preview";

    private readonly IFileCrypterSettingsService settingsService;
    private readonly IAppThemeService appThemeService;
    private readonly IFilePickerService? filePickerService;
    private readonly object autoSaveGate = new();
    private readonly TimeSpan defaultOutputDirectoryAutoSaveDelay;
    private FileCrypterSettings lastSavedSettings;
    private CancellationTokenSource? defaultOutputDirectoryDebounceCancellationTokenSource;
    private FileCrypterSettings? pendingAutoSaveSettings;
    private bool autoSaveLoopActive;
    private long autoSaveSessionId;
    private bool suppressAutoSave;
    private bool saveDefaultOutputDirectoryImmediately;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ReloadSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseDefaultOutputDirectoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetToDefaultsCommand))]
    private bool isRunning;

    [ObservableProperty]
    private FileCrypterThemePreference themePreference;

    [ObservableProperty]
    private bool enableCompressionByDefault;

    [ObservableProperty]
    private bool neverOverwriteExistingFilesByDefault;

    [ObservableProperty]
    private string defaultOutputDirectory;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string progressText;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string successMessage = string.Empty;

    [ObservableProperty]
    private bool isChangelogExpanded;

    public SettingsViewModel(
        IFileCrypterSettingsService settingsService,
        FileCrypterSettings initialSettings,
        IAppThemeService? appThemeService = null,
        IFilePickerService? filePickerService = null,
        string initialErrorMessage = "",
        TimeSpan? defaultOutputDirectoryAutoSaveDelay = null,
        IAppMetadataService? appMetadataService = null)
    {
        this.settingsService = settingsService;
        this.appThemeService = appThemeService ?? new NoOpAppThemeService();
        this.filePickerService = filePickerService;
        this.defaultOutputDirectoryAutoSaveDelay =
            defaultOutputDirectoryAutoSaveDelay ?? TimeSpan.FromMilliseconds(DefaultOutputDirectoryAutoSaveDelayMilliseconds);

        lastSavedSettings = NormalizeSettings(initialSettings);
        themePreference = lastSavedSettings.ThemePreference;
        enableCompressionByDefault = lastSavedSettings.EnableCompressionByDefault;
        neverOverwriteExistingFilesByDefault = lastSavedSettings.NeverOverwriteExistingFilesByDefault;
        defaultOutputDirectory = lastSavedSettings.DefaultOutputDirectory;
        progressText = IdleAutosaveMessage;
        errorMessage = initialErrorMessage;

        CurrentVersion = appMetadataService?.DisplayVersion ?? new AppMetadataService().DisplayVersion;
        string changelogVersion = string.IsNullOrWhiteSpace(CurrentVersion) ? DefaultChangelogVersion : CurrentVersion;
        ChangelogHeading = $"{changelogVersion} highlights";
        ChangelogEntries =
        [
            new HelpTopicViewModel(
                "Richer file pickers",
                "Encrypt, Decrypt, and Batch now treat file targets like real drop zones, then swap in compact file previews once you choose something."),
            new HelpTopicViewModel(
                "Shared desktop defaults",
                "Theme, compression, overwrite protection, and default output directory now save locally and feed back into every workflow as soon as you change them."),
            new HelpTopicViewModel(
                "Clearer workflow feedback",
                "Status-bar detail, copyable result text, and the Help/update surface now make it easier to confirm what happened or share the exact issue text when something goes wrong."),
        ];
    }

    public event Action<FileCrypterSettings>? SettingsSaved;

    public string Title => "Settings";

    public string SettingsPath => settingsService.SettingsPath;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);

    public string CurrentVersion { get; }

    public string ChangelogHeading { get; }

    public string ChangelogButtonText => IsChangelogExpanded ? "Hide changelog" : "View changelog";

    public IReadOnlyList<HelpTopicViewModel> ChangelogEntries { get; }

    public IReadOnlyList<FileCrypterThemePreference> ThemeOptions { get; } =
    [
        FileCrypterThemePreference.System,
        FileCrypterThemePreference.Light,
        FileCrypterThemePreference.Dark,
    ];

    public string ThemePreferenceDescription => ThemePreference switch
    {
        FileCrypterThemePreference.Light => "FileCrypter stays in light mode until you change this setting again.",
        FileCrypterThemePreference.Dark => "FileCrypter stays in dark mode until you change this setting again.",
        _ => "FileCrypter follows the platform's default light or dark appearance.",
    };

    public string CompressionDefaultDescription => EnableCompressionByDefault
        ? "New single-file encryption runs start with compression enabled."
        : "New single-file encryption runs start with compression disabled.";

    public string OverwriteProtectionDescription => NeverOverwriteExistingFilesByDefault
        ? "New encrypt, decrypt, and batch runs protect existing files by auto-renaming outputs."
        : "New runs can replace matching output names when you point them at the same path or folder.";

    public string DefaultOutputDirectoryDescription => string.IsNullOrWhiteSpace(DefaultOutputDirectory)
        ? "Leave this blank to keep using the source file's folder as the starting output location."
        : $"New workflows start from this output directory when it exists: {NormalizeDirectoryValue(DefaultOutputDirectory)}";

    partial void OnThemePreferenceChanged(FileCrypterThemePreference value)
    {
        FileCrypterThemePreference normalizedPreference = NormalizeThemePreference(value);
        if (ThemePreference != normalizedPreference)
        {
            ThemePreference = normalizedPreference;
            return;
        }

        appThemeService.ApplyTheme(normalizedPreference);
        OnPropertyChanged(nameof(ThemePreferenceDescription));

        if (suppressAutoSave)
        {
            return;
        }

        BeginAutoSaveFeedback();
        QueueImmediateAutoSave();
    }

    partial void OnEnableCompressionByDefaultChanged(bool value)
    {
        OnPropertyChanged(nameof(CompressionDefaultDescription));

        if (suppressAutoSave)
        {
            return;
        }

        BeginAutoSaveFeedback();
        QueueImmediateAutoSave();
    }

    partial void OnNeverOverwriteExistingFilesByDefaultChanged(bool value)
    {
        OnPropertyChanged(nameof(OverwriteProtectionDescription));

        if (suppressAutoSave)
        {
            return;
        }

        BeginAutoSaveFeedback();
        QueueImmediateAutoSave();
    }

    partial void OnDefaultOutputDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(DefaultOutputDirectoryDescription));

        if (suppressAutoSave)
        {
            return;
        }

        BeginAutoSaveFeedback();
        if (saveDefaultOutputDirectoryImmediately)
        {
            saveDefaultOutputDirectoryImmediately = false;
            QueueImmediateAutoSave();
            return;
        }

        DebounceDefaultOutputDirectoryAutoSave();
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnSuccessMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasSuccess));
    }

    partial void OnIsChangelogExpandedChanged(bool value)
    {
        OnPropertyChanged(nameof(ChangelogButtonText));
    }

    [RelayCommand]
    private void ToggleChangelog()
    {
        IsChangelogExpanded = !IsChangelogExpanded;
    }

    [RelayCommand(CanExecute = nameof(CanReloadSettings))]
    private async Task ReloadSettingsAsync()
    {
        CancelPendingAutoSaveWork();
        await RunSettingsActionAsync(
            async cancellationToken =>
            {
                FileCrypterSettings settings = await settingsService.LoadAsync(cancellationToken);
                ApplyPersistedSettings(settings, notifySettingsSaved: true);
                SuccessMessage = "Reloaded the current settings file.";
            },
            "Reloading settings...",
            "Settings reloaded.");
    }

    [RelayCommand(CanExecute = nameof(CanBrowseDefaultOutputDirectory))]
    private async Task BrowseDefaultOutputDirectoryAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        string? selectedPath = await filePickerService.PickOpenFolderAsync(
            "Choose default output directory",
            CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            saveDefaultOutputDirectoryImmediately = true;
            DefaultOutputDirectory = NormalizeDirectoryValue(Path.GetFullPath(selectedPath));
        }
    }

    [RelayCommand(CanExecute = nameof(CanResetToDefaults))]
    private async Task ResetToDefaultsAsync()
    {
        CancelPendingAutoSaveWork();
        await RunSettingsActionAsync(
            async cancellationToken =>
            {
                FileCrypterSettings settings = new();
                await settingsService.SaveAsync(settings, cancellationToken);
                ApplyPersistedSettings(settings, notifySettingsSaved: true);
                SuccessMessage = "Reset every shared setting back to the default FileCrypter behavior.";
            },
            "Resetting settings...",
            "Settings reset to defaults.");
    }

    private bool CanReloadSettings()
    {
        return !IsRunning;
    }

    private bool CanBrowseDefaultOutputDirectory()
    {
        return !IsRunning && filePickerService is not null;
    }

    private bool CanResetToDefaults()
    {
        return !IsRunning;
    }

    private void BeginAutoSaveFeedback()
    {
        CancelPendingSuccessMessage();
        ErrorMessage = string.Empty;
        StatusText = "Settings";
        ProgressText = "Saving settings...";
    }

    private void QueueImmediateAutoSave()
    {
        CancelDefaultOutputDirectoryAutoSave();
        QueueAutoSaveSnapshot();
    }

    private void DebounceDefaultOutputDirectoryAutoSave()
    {
        CancelDefaultOutputDirectoryAutoSave();
        var cancellationTokenSource = new CancellationTokenSource();
        defaultOutputDirectoryDebounceCancellationTokenSource = cancellationTokenSource;
        _ = DebounceDefaultOutputDirectoryAutoSaveAsync(cancellationTokenSource.Token);
    }

    private async Task DebounceDefaultOutputDirectoryAutoSaveAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(defaultOutputDirectoryAutoSaveDelay, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        QueueAutoSaveSnapshot();
    }

    private void QueueAutoSaveSnapshot()
    {
        FileCrypterSettings settings;
        try
        {
            settings = CreateCurrentSettings();
        }
        catch (Exception exception)
        {
            HandleAutoSaveFailure(exception);
            return;
        }

        bool shouldStartLoop;
        lock (autoSaveGate)
        {
            pendingAutoSaveSettings = settings;
            shouldStartLoop = !autoSaveLoopActive;
            if (shouldStartLoop)
            {
                autoSaveLoopActive = true;
            }
        }

        if (shouldStartLoop)
        {
            _ = RunAutoSaveLoopAsync();
        }
    }

    private async Task RunAutoSaveLoopAsync()
    {
        while (true)
        {
            FileCrypterSettings? pendingSettings;
            long saveSessionId;
            lock (autoSaveGate)
            {
                pendingSettings = pendingAutoSaveSettings;
                saveSessionId = autoSaveSessionId;
                pendingAutoSaveSettings = null;
                if (pendingSettings is null)
                {
                    autoSaveLoopActive = false;
                    IsRunning = false;
                    StatusText = "Ready";
                    return;
                }
            }

            IsRunning = true;
            StatusText = "Settings";
            ProgressText = "Saving settings...";

            try
            {
                await settingsService.SaveAsync(pendingSettings, CancellationToken.None);
            }
            catch (Exception exception)
            {
                lock (autoSaveGate)
                {
                    pendingAutoSaveSettings = null;
                    autoSaveLoopActive = false;
                }

                HandleAutoSaveFailure(exception);
                return;
            }

            if (ShouldIgnoreAutoSaveCompletion(saveSessionId))
            {
                continue;
            }

            CompleteAutoSave(pendingSettings);
        }
    }

    private async Task RunSettingsActionAsync(
        Func<CancellationToken, Task> action,
        string progressMessage,
        string completionMessage)
    {
        IsRunning = true;
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        StatusText = "Settings";
        ProgressText = progressMessage;

        try
        {
            await action(CancellationToken.None);
            StatusText = "Ready";
            ProgressText = completionMessage;
        }
        catch (Exception exception)
        {
            ErrorMessage = $"Settings error: {exception.Message}";
            StatusText = "Ready";
            ProgressText = "Settings update failed.";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private void CompleteAutoSave(FileCrypterSettings settings)
    {
        lastSavedSettings = NormalizeSettings(settings);
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        StatusText = "Ready";
        ProgressText = "Settings saved automatically.";
        SettingsSaved?.Invoke(lastSavedSettings);
    }

    private void HandleAutoSaveFailure(Exception exception)
    {
        bool restorePersistedSettingsAfterFailure = HasActiveAutoSaveLoop();
        CancelPendingAutoSaveWork(
            invalidateInFlightSave: true,
            restoreLastSavedSettings: restorePersistedSettingsAfterFailure);
        ApplyPersistedSettings(lastSavedSettings, notifySettingsSaved: false);
        ErrorMessage = $"Settings error: {exception.Message}";
        SuccessMessage = string.Empty;
        StatusText = "Ready";
        ProgressText = "Settings update failed.";
        IsRunning = false;
    }

    private void CancelPendingSuccessMessage()
    {
        SuccessMessage = string.Empty;
    }

    private void CancelPendingAutoSaveWork(
        bool invalidateInFlightSave = false,
        bool restoreLastSavedSettings = false)
    {
        CancelDefaultOutputDirectoryAutoSave();
        bool shouldStartLoop = false;
        lock (autoSaveGate)
        {
            pendingAutoSaveSettings = restoreLastSavedSettings ? lastSavedSettings : null;
            if (invalidateInFlightSave)
            {
                autoSaveSessionId++;
            }

            if (restoreLastSavedSettings && !autoSaveLoopActive)
            {
                autoSaveLoopActive = true;
                shouldStartLoop = true;
            }
        }

        if (shouldStartLoop)
        {
            _ = RunAutoSaveLoopAsync();
        }
    }

    private bool ShouldIgnoreAutoSaveCompletion(long saveSessionId)
    {
        lock (autoSaveGate)
        {
            return saveSessionId != autoSaveSessionId;
        }
    }

    private bool HasActiveAutoSaveLoop()
    {
        lock (autoSaveGate)
        {
            return autoSaveLoopActive;
        }
    }

    private void CancelDefaultOutputDirectoryAutoSave()
    {
        if (defaultOutputDirectoryDebounceCancellationTokenSource is null)
        {
            return;
        }

        defaultOutputDirectoryDebounceCancellationTokenSource.Cancel();
        defaultOutputDirectoryDebounceCancellationTokenSource.Dispose();
        defaultOutputDirectoryDebounceCancellationTokenSource = null;
    }

    private FileCrypterSettings CreateCurrentSettings()
    {
        string normalizedDefaultOutputDirectory = NormalizeDirectoryValue(DefaultOutputDirectory);
        if (!string.IsNullOrWhiteSpace(normalizedDefaultOutputDirectory) &&
            !Directory.Exists(normalizedDefaultOutputDirectory))
        {
            throw new DirectoryNotFoundException(
                $"The default output directory does not exist: {normalizedDefaultOutputDirectory}");
        }

        return new FileCrypterSettings
        {
            ThemePreference = NormalizeThemePreference(ThemePreference),
            EnableCompressionByDefault = EnableCompressionByDefault,
            NeverOverwriteExistingFilesByDefault = NeverOverwriteExistingFilesByDefault,
            DefaultOutputDirectory = normalizedDefaultOutputDirectory,
        }.Normalize();
    }

    private void ApplyPersistedSettings(FileCrypterSettings settings, bool notifySettingsSaved)
    {
        FileCrypterSettings normalizedSettings = NormalizeSettings(settings);
        lastSavedSettings = normalizedSettings;

        suppressAutoSave = true;
        saveDefaultOutputDirectoryImmediately = false;
        try
        {
            ThemePreference = normalizedSettings.ThemePreference;
            EnableCompressionByDefault = normalizedSettings.EnableCompressionByDefault;
            NeverOverwriteExistingFilesByDefault = normalizedSettings.NeverOverwriteExistingFilesByDefault;
            DefaultOutputDirectory = normalizedSettings.DefaultOutputDirectory;
        }
        finally
        {
            suppressAutoSave = false;
        }

        appThemeService.ApplyTheme(normalizedSettings.ThemePreference);
        if (notifySettingsSaved)
        {
            SettingsSaved?.Invoke(normalizedSettings);
        }
    }

    private static FileCrypterSettings NormalizeSettings(FileCrypterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Normalize();
    }

    private static string NormalizeDirectoryValue(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
    }

    private static FileCrypterThemePreference NormalizeThemePreference(FileCrypterThemePreference preference)
    {
        return Enum.IsDefined(preference) ? preference : FileCrypterThemePreference.System;
    }
}

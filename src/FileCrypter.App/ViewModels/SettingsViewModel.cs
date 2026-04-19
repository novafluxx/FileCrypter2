using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IWorkflowStatusViewModel
{
    private readonly IFileCrypterSettingsService settingsService;
    private readonly IFilePickerService? filePickerService;
    private bool savedEnableCompressionByDefault;
    private bool savedNeverOverwriteExistingFilesByDefault;
    private string savedDefaultOutputDirectory;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReloadSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseDefaultOutputDirectoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(ResetToDefaultsCommand))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
    private bool enableCompressionByDefault;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
    private bool neverOverwriteExistingFilesByDefault;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
    private string defaultOutputDirectory;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string progressText;

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    private string successMessage = string.Empty;

    public SettingsViewModel(
        IFileCrypterSettingsService settingsService,
        FileCrypterSettings initialSettings,
        IFilePickerService? filePickerService = null,
        string initialErrorMessage = "")
    {
        this.settingsService = settingsService;
        this.filePickerService = filePickerService;
        progressText = settingsService.SettingsPath;
        savedEnableCompressionByDefault = initialSettings.EnableCompressionByDefault;
        savedNeverOverwriteExistingFilesByDefault = initialSettings.NeverOverwriteExistingFilesByDefault;
        savedDefaultOutputDirectory = NormalizeDirectoryValue(initialSettings.DefaultOutputDirectory);
        enableCompressionByDefault = initialSettings.EnableCompressionByDefault;
        neverOverwriteExistingFilesByDefault = initialSettings.NeverOverwriteExistingFilesByDefault;
        defaultOutputDirectory = savedDefaultOutputDirectory;
        errorMessage = initialErrorMessage;
    }

    public event Action<FileCrypterSettings>? SettingsSaved;

    public string Title => "Settings";

    public string SettingsPath => settingsService.SettingsPath;

    public bool HasPendingChanges =>
        EnableCompressionByDefault != savedEnableCompressionByDefault ||
        NeverOverwriteExistingFilesByDefault != savedNeverOverwriteExistingFilesByDefault ||
        !string.Equals(
            NormalizeDirectoryValue(DefaultOutputDirectory),
            savedDefaultOutputDirectory,
            StringComparison.Ordinal);

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);

    public string CompressionDefaultDescription => EnableCompressionByDefault
        ? "New single-file encryption runs start with compression enabled."
        : "New single-file encryption runs start with compression disabled.";

    public string OverwriteProtectionDescription => NeverOverwriteExistingFilesByDefault
        ? "New encrypt, decrypt, and batch runs protect existing files by auto-renaming outputs."
        : "New runs can replace matching output names when you point them at the same path or folder.";

    public string DefaultOutputDirectoryDescription => string.IsNullOrWhiteSpace(DefaultOutputDirectory)
        ? "Leave this blank to keep using the source file's folder as the starting output location."
        : $"New workflows start from this output directory when it exists: {NormalizeDirectoryValue(DefaultOutputDirectory)}";

    partial void OnEnableCompressionByDefaultChanged(bool value)
    {
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(CompressionDefaultDescription));
        SuccessMessage = string.Empty;
    }

    partial void OnNeverOverwriteExistingFilesByDefaultChanged(bool value)
    {
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(OverwriteProtectionDescription));
        SuccessMessage = string.Empty;
    }

    partial void OnDefaultOutputDirectoryChanged(string value)
    {
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(DefaultOutputDirectoryDescription));
        SuccessMessage = string.Empty;
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnSuccessMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasSuccess));
    }

    [RelayCommand(CanExecute = nameof(CanSaveSettings))]
    private async Task SaveSettingsAsync()
    {
        await SaveOrReloadAsync(
            async cancellationToken =>
            {
                FileCrypterSettings settings = CreateCurrentSettings();

                await settingsService.SaveAsync(settings, cancellationToken);
                ApplySavedSettings(settings);
                SuccessMessage = "Saved. New workflows now use the updated shared defaults.";
            },
            "Saving settings...",
            "Settings saved.");
    }

    [RelayCommand(CanExecute = nameof(CanReloadSettings))]
    private async Task ReloadSettingsAsync()
    {
        await SaveOrReloadAsync(
            async cancellationToken =>
            {
                FileCrypterSettings settings = await settingsService.LoadAsync(cancellationToken);
                ApplySavedSettings(settings);
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
            DefaultOutputDirectory = NormalizeDirectoryValue(Path.GetFullPath(selectedPath));
        }
    }

    [RelayCommand(CanExecute = nameof(CanResetToDefaults))]
    private async Task ResetToDefaultsAsync()
    {
        await SaveOrReloadAsync(
            async cancellationToken =>
            {
                FileCrypterSettings settings = new();
                await settingsService.SaveAsync(settings, cancellationToken);
                ApplySavedSettings(settings);
                SuccessMessage = "Reset every shared setting back to the default FileCrypter behavior.";
            },
            "Resetting settings...",
            "Settings reset to defaults.");
    }

    private bool CanSaveSettings()
    {
        return !IsRunning && HasPendingChanges;
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

    private async Task SaveOrReloadAsync(
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
            EnableCompressionByDefault = EnableCompressionByDefault,
            NeverOverwriteExistingFilesByDefault = NeverOverwriteExistingFilesByDefault,
            DefaultOutputDirectory = normalizedDefaultOutputDirectory,
        };
    }

    private void ApplySavedSettings(FileCrypterSettings settings)
    {
        savedEnableCompressionByDefault = settings.EnableCompressionByDefault;
        savedNeverOverwriteExistingFilesByDefault = settings.NeverOverwriteExistingFilesByDefault;
        savedDefaultOutputDirectory = NormalizeDirectoryValue(settings.DefaultOutputDirectory);
        EnableCompressionByDefault = settings.EnableCompressionByDefault;
        NeverOverwriteExistingFilesByDefault = settings.NeverOverwriteExistingFilesByDefault;
        DefaultOutputDirectory = savedDefaultOutputDirectory;
        OnPropertyChanged(nameof(HasPendingChanges));
        SettingsSaved?.Invoke(settings);
    }

    private static string NormalizeDirectoryValue(string? path)
    {
        return string.IsNullOrWhiteSpace(path) ? string.Empty : path.Trim();
    }
}

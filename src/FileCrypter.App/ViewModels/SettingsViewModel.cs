using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.ViewModels;

public sealed partial class SettingsViewModel : ViewModelBase, IWorkflowStatusViewModel
{
    private readonly IFileCrypterSettingsService settingsService;
    private bool savedEnableCompressionByDefault;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(ReloadSettingsCommand))]
    [NotifyCanExecuteChangedFor(nameof(RestoreDefaultCommand))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveSettingsCommand))]
    private bool enableCompressionByDefault;

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
        string initialErrorMessage = "")
    {
        this.settingsService = settingsService;
        progressText = settingsService.SettingsPath;
        savedEnableCompressionByDefault = initialSettings.EnableCompressionByDefault;
        enableCompressionByDefault = initialSettings.EnableCompressionByDefault;
        errorMessage = initialErrorMessage;
    }

    public event Action<FileCrypterSettings>? SettingsSaved;

    public string Title => "Settings";

    public string SettingsPath => settingsService.SettingsPath;

    public bool HasPendingChanges => EnableCompressionByDefault != savedEnableCompressionByDefault;

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasSuccess => !string.IsNullOrWhiteSpace(SuccessMessage);

    public string CompressionDefaultDescription => EnableCompressionByDefault
        ? "New single-file encryption runs start with compression enabled."
        : "New single-file encryption runs start with compression disabled.";

    partial void OnEnableCompressionByDefaultChanged(bool value)
    {
        OnPropertyChanged(nameof(HasPendingChanges));
        OnPropertyChanged(nameof(CompressionDefaultDescription));
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
                FileCrypterSettings settings = new()
                {
                    EnableCompressionByDefault = EnableCompressionByDefault,
                };

                await settingsService.SaveAsync(settings, cancellationToken);
                savedEnableCompressionByDefault = settings.EnableCompressionByDefault;
                OnPropertyChanged(nameof(HasPendingChanges));
                SuccessMessage = "Saved. The Encrypt page now uses the new compression default.";
                SettingsSaved?.Invoke(settings);
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
                savedEnableCompressionByDefault = settings.EnableCompressionByDefault;
                EnableCompressionByDefault = settings.EnableCompressionByDefault;
                OnPropertyChanged(nameof(HasPendingChanges));
                SuccessMessage = "Reloaded the current settings file.";
                SettingsSaved?.Invoke(settings);
            },
            "Reloading settings...",
            "Settings reloaded.");
    }

    [RelayCommand(CanExecute = nameof(CanRestoreDefault))]
    private void RestoreDefault()
    {
        ErrorMessage = string.Empty;
        SuccessMessage = string.Empty;
        EnableCompressionByDefault = false;
    }

    private bool CanSaveSettings()
    {
        return !IsRunning && HasPendingChanges;
    }

    private bool CanReloadSettings()
    {
        return !IsRunning;
    }

    private bool CanRestoreDefault()
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
}

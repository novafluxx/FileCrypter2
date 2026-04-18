using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;

namespace FileCrypter.App.ViewModels;

public sealed partial class HelpViewModel : ViewModelBase, IWorkflowStatusViewModel
{
    private readonly IAppUpdateService appUpdateService;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CheckForUpdatesCommand))]
    private bool isCheckingForUpdates;

    [ObservableProperty]
    private string updateSummaryText = string.Empty;

    [ObservableProperty]
    private string updateDetailText = string.Empty;

    [ObservableProperty]
    private string lastCheckedText = string.Empty;

    [ObservableProperty]
    private string updateSummaryBrush = "#d7e7e3";

    public HelpViewModel(
        IAppMetadataService appMetadataService,
        IAppUpdateService appUpdateService)
    {
        ArgumentNullException.ThrowIfNull(appMetadataService);
        this.appUpdateService = appUpdateService ?? throw new ArgumentNullException(nameof(appUpdateService));

        CurrentVersion = appMetadataService.DisplayVersion;
        FormatVersionText = $"Encrypted file format v{appMetadataService.FormatVersion}";

        WorkflowTopics =
        [
            new HelpTopicViewModel(
                "Encrypt one file",
                "Choose one source file, keep the password memorable, and leave the output blank if you want FileCrypter to suggest a safe .encrypted destination."),
            new HelpTopicViewModel(
                "Decrypt one file",
                "Choose the encrypted file, keep the original password handy, and supply the matching key file only if that file was protected with one."),
            new HelpTopicViewModel(
                "Batch individual files",
                "Use individual-file mode when every selected file should produce its own output. Batch encryption turns compression on automatically and keeps per-file failures isolated."),
            new HelpTopicViewModel(
                "Batch archive mode",
                "Use archive mode when several files should become one .tar.zst.encrypted output, or when one encrypted archive needs to be decrypted and extracted back into a folder."),
            new HelpTopicViewModel(
                "Compression and large files",
                "Compression helps most with text and documents. FileCrypter streams work in chunks so large files do not need to fit in memory all at once."),
            new HelpTopicViewModel(
                "Key files and recovery",
                "A key file acts as an optional second factor. If you encrypt with one, you must keep the exact same unchanged key file to decrypt later."),
        ];

        TroubleshootingTopics =
        [
            new HelpTopicViewModel(
                "Wrong password or wrong key file",
                "Try the exact password used during encryption and confirm the key file is the same original file, not a copy that was edited or regenerated."),
            new HelpTopicViewModel(
                "Corrupted, truncated, or unsupported files",
                "If FileCrypter reports malformed, tampered, truncated, or unsupported encrypted content, re-copy the file from a known-good source before trying again."),
            new HelpTopicViewModel(
                "Output folders and overwrite handling",
                "Choose an existing output folder first. With overwrite protection enabled, FileCrypter auto-renames new outputs instead of replacing files that already exist."),
            new HelpTopicViewModel(
                "Too many files or picker issues",
                "Individual-file batch runs stop at 1000 files. If a native picker does not return what you expected, re-open it and confirm the folder or file list before starting the run."),
        ];

        SafetyTopics =
        [
            new HelpTopicViewModel(
                "Recovery limits",
                "FileCrypter cannot recover forgotten passwords or lost or changed key files."),
            new HelpTopicViewModel(
                "Local-only handling",
                "Passwords, key-file bytes, and file contents stay on this device. The app stores only local settings such as the compression default."),
            new HelpTopicViewModel(
                "Format compatibility",
                "The current desktop workflows target the documented v1 encrypted format and reject unsupported or suspicious metadata before decrypting."),
        ];

        ApplyUpdateStatus(this.appUpdateService.GetCurrentStatus());
    }

    public string Title => "Help and recovery";

    public string CurrentVersion { get; }

    public string FormatVersionText { get; }

    public IReadOnlyList<HelpTopicViewModel> WorkflowTopics { get; }

    public IReadOnlyList<HelpTopicViewModel> TroubleshootingTopics { get; }

    public IReadOnlyList<HelpTopicViewModel> SafetyTopics { get; }

    public string StatusText => IsCheckingForUpdates ? "Checking for updates" : "Help and recovery";

    public string ProgressText => IsCheckingForUpdates
        ? "Looking for update information..."
        : UpdateSummaryText;

    public bool HasLastCheckedText => !string.IsNullOrWhiteSpace(LastCheckedText);

    partial void OnIsCheckingForUpdatesChanged(bool value)
    {
        OnPropertyChanged(nameof(StatusText));
        OnPropertyChanged(nameof(ProgressText));
    }

    partial void OnUpdateSummaryTextChanged(string value)
    {
        OnPropertyChanged(nameof(ProgressText));
    }

    partial void OnLastCheckedTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasLastCheckedText));
    }

    [RelayCommand(CanExecute = nameof(CanCheckForUpdates))]
    private async Task CheckForUpdatesAsync()
    {
        IsCheckingForUpdates = true;

        try
        {
            AppUpdateCheckResult result = await appUpdateService.CheckForUpdatesAsync(CancellationToken.None);
            ApplyUpdateStatus(result);
        }
        catch (Exception exception)
        {
            ApplyUpdateStatus(new AppUpdateCheckResult(
                AppUpdateStatus.Failed,
                "FileCrypter could not check for updates.",
                $"The update status could not be refreshed: {exception.Message}",
                DateTimeOffset.Now));
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    private bool CanCheckForUpdates()
    {
        return !IsCheckingForUpdates;
    }

    private void ApplyUpdateStatus(AppUpdateCheckResult result)
    {
        UpdateSummaryText = result.AvailableVersion is { Length: > 0 }
            ? $"{result.SummaryText} ({result.AvailableVersion})"
            : result.SummaryText;
        UpdateDetailText = result.DetailText;
        LastCheckedText = result.CheckedAt is null
            ? string.Empty
            : $"Last checked: {result.CheckedAt.Value.LocalDateTime:g}";
        UpdateSummaryBrush = result.Status switch
        {
            AppUpdateStatus.UpToDate => "#9cc4ff",
            AppUpdateStatus.UpdateAvailable => "#b8e6c2",
            AppUpdateStatus.Failed => "#f0a0a4",
            _ => "#f0d090",
        };
    }
}

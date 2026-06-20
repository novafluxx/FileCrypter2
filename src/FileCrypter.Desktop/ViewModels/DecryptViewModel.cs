using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.Desktop.Services;
using FileCrypter.Core;
using FileCrypter.Core.Settings;
using System.Diagnostics;
using System.Text;

namespace FileCrypter.Desktop.ViewModels;

public sealed partial class DecryptViewModel : ViewModelBase, IWorkflowStatusViewModel, IWorkflowToastSource
{
    private const string DefaultEncryptedSuffix = ".encrypted";
    private const string DefaultDecryptedSuffix = ".decrypted";

    private int activeProgressRunId;
    private readonly IFileCrypterWorkflowService workflowService;
    private readonly IFilePickerService? filePickerService;
    private readonly IClipboardService? clipboardService;
    private readonly IPathRevealService pathRevealService;
    private readonly object progressGate = new();
    private readonly Stopwatch runStopwatch = new();
    private string defaultOutputDirectory = string.Empty;
    private OutputPathOrigin outputPathOrigin;
    private bool isUpdatingOutputPathInternally;
    // Argon2id key derivation is CPU-bound and cannot observe the token, so cancellation
    // takes effect after derivation completes, during the chunked IO phase.
    private CancellationTokenSource? runCancellation;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDecryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(CancelCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseSourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseOutputCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSourceCommand))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDecryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSourceCommand))]
    private string sourcePath = string.Empty;

    [ObservableProperty]
    private FileSelectionPreviewViewModel? sourcePreview;

    [ObservableProperty]
    private string outputPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDecryptCommand))]
    private string password = string.Empty;

    [ObservableProperty]
    private bool showPassword;

    [ObservableProperty]
    private bool neverOverwriteExistingFiles = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearKeyFileCommand))]
    private string keyFilePath = string.Empty;

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string progressText = "No encrypted file selected";

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyErrorCommand))]
    private string visibleErrorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyResultCommand))]
    private string resultPath = string.Empty;

    [ObservableProperty]
    private bool isAdvancedOptionsExpanded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevealFooterPathCommand))]
    private string footerActionText = string.Empty;

    public DecryptViewModel(
        IFileCrypterWorkflowService workflowService,
        IFilePickerService? filePickerService = null,
        IClipboardService? clipboardService = null,
        IPathRevealService? pathRevealService = null)
    {
        this.workflowService = workflowService;
        this.filePickerService = filePickerService;
        this.clipboardService = clipboardService;
        this.pathRevealService = pathRevealService ?? new NoOpPathRevealService();
    }

    public DecryptViewModel(
        IFileCrypterWorkflowService workflowService,
        FileCrypterSettings initialSettings,
        IFilePickerService? filePickerService = null,
        IClipboardService? clipboardService = null,
        IPathRevealService? pathRevealService = null)
    {
        this.workflowService = workflowService;
        this.filePickerService = filePickerService;
        this.clipboardService = clipboardService;
        this.pathRevealService = pathRevealService ?? new NoOpPathRevealService();
        ApplySettings(initialSettings);
    }

    public event EventHandler<WorkflowToastNotification>? ToastNotificationRequested;

    public string Title => "Decrypt a file";

    public string HeroTitle => "Decrypt a file";

    public bool HasFooterAction => !string.IsNullOrWhiteSpace(FooterActionText);

    public System.Windows.Input.ICommand FooterActionCommand => RevealFooterPathCommand;

    public bool HasError => !string.IsNullOrWhiteSpace(VisibleErrorMessage);

    public bool HasResult => !string.IsNullOrWhiteSpace(ResultPath);

    public bool ShowReadyAction => GetIsReady();

    public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SourcePath);

    public bool ShowEmptySourceState => !HasSelectedFile;

    public bool HasKeyFileChoice => !string.IsNullOrWhiteSpace(KeyFilePath);

    public bool ShowMaskedPasswordInput => !ShowPassword;

    public string PasswordVisibilityActionText => ShowPassword ? "Hide" : "Show";

    public string KeyFileChoiceStatusText => HasKeyFileChoice
        ? "Using the selected key file as the optional second factor."
        : "No key file selected. Decryption will use only the password unless the file requires one.";

    public string DetailsPanelTitle => HasResult
        ? "RESTORED"
        : IsRunning
            ? "DECRYPTING"
            : HasSelectedFile
                ? "FILE DETAILS"
                : "READY";

    public string DetailsPanelBody => HasResult
        ? ResultPath
        : HasSelectedFile && SourcePreview is not null
            ? $"{SourcePreview.DisplayName} is staged for local decryption and safe output naming."
            : "Drop an encrypted file to inspect its filename, output target, and key-file requirements.";

    public string OutputDisplayText => string.IsNullOrWhiteSpace(OutputPath)
        ? "Auto-generated from input filename..."
        : OutputPath;

    public bool ApplyDroppedSourcePaths(IEnumerable<string> droppedPaths)
    {
        ArgumentNullException.ThrowIfNull(droppedPaths);

        if (IsRunning)
        {
            return false;
        }

        string? selectedPath = droppedPaths.FirstOrDefault(path => !string.IsNullOrWhiteSpace(path));
        if (string.IsNullOrWhiteSpace(selectedPath))
        {
            return false;
        }

        SourcePath = Path.GetFullPath(selectedPath);
        return true;
    }

    public void ApplySettings(FileCrypterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        NeverOverwriteExistingFiles = settings.NeverOverwriteExistingFilesByDefault;
        defaultOutputDirectory = GetUsableDefaultOutputDirectory(settings.DefaultOutputDirectory);
        RefreshSuggestedOutputPath();
        ResetReadyFooter();
    }

    partial void OnSourcePathChanged(string value)
    {
        SourcePreview = string.IsNullOrWhiteSpace(value)
            ? null
            : new FileSelectionPreviewViewModel(value);
        ClearVisibleError();
        OnPropertyChanged(nameof(HasSelectedFile));
        OnPropertyChanged(nameof(ShowEmptySourceState));
        OnPropertyChanged(nameof(DetailsPanelTitle));
        OnPropertyChanged(nameof(DetailsPanelBody));
        OnPropertyChanged(nameof(ShowReadyAction));
        RefreshSuggestedOutputPath();
        ResetReadyFooter();
    }

    partial void OnOutputPathChanged(string value)
    {
        if (!isUpdatingOutputPathInternally)
        {
            outputPathOrigin = string.IsNullOrWhiteSpace(value)
                ? OutputPathOrigin.None
                : OutputPathOrigin.Manual;
        }

        ClearVisibleError();
        OnPropertyChanged(nameof(OutputDisplayText));
        ResetReadyFooter();
    }

    partial void OnPasswordChanged(string value)
    {
        ClearVisibleError();
        OnPropertyChanged(nameof(ShowReadyAction));
        ResetReadyFooter();
    }

    partial void OnShowPasswordChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowMaskedPasswordInput));
        OnPropertyChanged(nameof(PasswordVisibilityActionText));
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(DetailsPanelTitle));
        OnPropertyChanged(nameof(DetailsPanelBody));
        OnPropertyChanged(nameof(ShowReadyAction));
        ClearSourceCommand.NotifyCanExecuteChanged();
    }

    partial void OnKeyFilePathChanged(string value)
    {
        ClearVisibleError();
        OnPropertyChanged(nameof(HasKeyFileChoice));
        OnPropertyChanged(nameof(KeyFileChoiceStatusText));
        ResetReadyFooter();
    }

    partial void OnVisibleErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnResultPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(DetailsPanelTitle));
        OnPropertyChanged(nameof(DetailsPanelBody));
        OnPropertyChanged(nameof(ShowReadyAction));
    }

    partial void OnNeverOverwriteExistingFilesChanged(bool value)
    {
        ResetReadyFooter();
    }

    partial void OnFooterActionTextChanged(string value)
    {
        OnPropertyChanged(nameof(HasFooterAction));
    }

    [RelayCommand]
    private void TogglePasswordVisibility()
    {
        ShowPassword = !ShowPassword;
    }

    [RelayCommand]
    private void ToggleAdvancedOptions()
    {
        IsAdvancedOptionsExpanded = !IsAdvancedOptionsExpanded;
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseSourceAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        try
        {
            string? selectedPath = await filePickerService.PickOpenFileAsync(
                "Choose an encrypted file to decrypt",
                CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                SourcePath = Path.GetFullPath(selectedPath);
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HandlePickerFailure(exception);
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearSource))]
    private void ClearSource()
    {
        SourcePath = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseOutputAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        try
        {
            string? selectedPath = await filePickerService.PickSaveFileAsync(
                "Choose decrypted output file",
                GetSuggestedOutputFileName(),
                CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                OutputPath = selectedPath;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HandlePickerFailure(exception);
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowseKeyFile))]
    private async Task BrowseKeyFileAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        try
        {
            string? selectedPath = await filePickerService.PickOpenFileAsync(
                "Choose the matching key file",
                CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                KeyFilePath = selectedPath;
            }
        }
        catch (OperationCanceledException)
        {
        }
        catch (Exception exception)
        {
            HandlePickerFailure(exception);
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearKeyFile))]
    private void ClearKeyFile()
    {
        KeyFilePath = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanStartDecrypt))]
    private async Task StartDecryptAsync()
    {
        int progressRunId = BeginProgressRun();
        IsRunning = true;
        runCancellation = new CancellationTokenSource();
        runStopwatch.Restart();
        ErrorMessage = string.Empty;
        ResultPath = string.Empty;
        ProgressPercent = 0;
        FooterActionText = string.Empty;
        ProgressText = "Preparing encrypted file...";
        StatusText = "Decrypting";

        try
        {
            string password = Password;
            Password = string.Empty;

            DecryptFileRequest request = new(
                SourcePath,
                string.IsNullOrWhiteSpace(OutputPath) ? null : OutputPath,
                password,
                NeverOverwriteExistingFiles,
                string.IsNullOrWhiteSpace(KeyFilePath) ? null : KeyFilePath);

            Progress<FileCrypterProgress> progress = new(value => ReportProgress(progressRunId, value));
            DecryptFileResult result = await workflowService.DecryptFileAsync(
                request,
                progress,
                runCancellation.Token);

            lock (progressGate)
            {
                CompleteProgressRun(progressRunId);
                runStopwatch.Stop();
                ResultPath = result.OutputPath;
                Password = string.Empty;
                ProgressPercent = 100;
                ProgressText = "Saved to";
                StatusText = $"Decrypted in {WorkflowStatusTextFormatter.FormatElapsed(runStopwatch.Elapsed)}";
                FooterActionText = result.OutputPath;
                RaiseToast(
                    WorkflowToastKind.Success,
                    "Decryption complete",
                    "Restored file saved.",
                    result.OutputPath);
            }
        }
        catch (OperationCanceledException)
        {
            lock (progressGate)
            {
                CompleteProgressRun(progressRunId);
                runStopwatch.Stop();
                Password = string.Empty;
                ProgressPercent = 0;
                ProgressText = "Decryption cancelled.";
                StatusText = "Cancelled";
                FooterActionText = string.Empty;
            }
        }
        catch (Exception exception)
        {
            lock (progressGate)
            {
                CompleteProgressRun(progressRunId);
                runStopwatch.Stop();
                string message = WorkflowErrorMessageFormatter.GetTroubleshootingMessage(exception);
                ErrorMessage = message;
                VisibleErrorMessage = message;
                Password = string.Empty;
                ProgressText = WorkflowStatusTextFormatter.SummarizeStatusDetail(message);
                StatusText = "Decryption failed";
                FooterActionText = string.Empty;
            }
        }
        finally
        {
            IsRunning = false;
            runCancellation?.Dispose();
            runCancellation = null;
        }
    }

    [RelayCommand(CanExecute = nameof(CanCancel))]
    private void Cancel()
    {
        runCancellation?.Cancel();
    }

    [RelayCommand(CanExecute = nameof(CanCopyError))]
    private Task CopyErrorAsync()
    {
        return CopyTextAsync(VisibleErrorMessage, "Copied issue details.");
    }

    [RelayCommand(CanExecute = nameof(CanCopyResult))]
    private Task CopyResultAsync()
    {
        return CopyTextAsync(BuildResultClipboardText(), "Copied result details.");
    }

    [RelayCommand(CanExecute = nameof(CanRevealFooterPath))]
    private async Task RevealFooterPathAsync()
    {
        bool revealed = await pathRevealService.TryRevealPathAsync(FooterActionText, CancellationToken.None).ConfigureAwait(true);
        if (!revealed)
        {
            ReportPathRevealFailure();
        }
    }

    private bool CanBrowse()
    {
        return !IsRunning;
    }

    private bool CanCancel()
    {
        return IsRunning;
    }

    private bool CanClearSource()
    {
        return !IsRunning && HasSelectedFile;
    }

    private bool CanBrowseKeyFile()
    {
        return !IsRunning;
    }

    private bool CanClearKeyFile()
    {
        return !IsRunning && HasKeyFileChoice;
    }

    private bool CanStartDecrypt()
    {
        return GetIsReady();
    }

    private bool GetIsReady()
    {
        return !IsRunning &&
            !string.IsNullOrWhiteSpace(SourcePath) &&
            !string.IsNullOrWhiteSpace(Password);
    }

    private bool CanCopyError()
    {
        return clipboardService is not null && HasError;
    }

    private void ClearVisibleError()
    {
        if (IsRunning || string.IsNullOrWhiteSpace(VisibleErrorMessage))
        {
            return;
        }

        VisibleErrorMessage = string.Empty;
        ErrorMessage = string.Empty;
    }

    private bool CanCopyResult()
    {
        return clipboardService is not null && HasResult;
    }

    private bool CanRevealFooterPath()
    {
        return HasFooterAction;
    }

    private string? GetSuggestedOutputFileName()
    {
        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            return null;
        }

        string inputFileName = Path.GetFileName(SourcePath);
        return inputFileName.EndsWith(DefaultEncryptedSuffix, StringComparison.OrdinalIgnoreCase)
            ? inputFileName[..^DefaultEncryptedSuffix.Length]
            : inputFileName + DefaultDecryptedSuffix;
    }

    private void RefreshSuggestedOutputPath()
    {
        if (string.IsNullOrWhiteSpace(SourcePath))
        {
            if (outputPathOrigin == OutputPathOrigin.SettingsDefault)
            {
                SetOutputPath(string.Empty, OutputPathOrigin.None);
            }

            return;
        }

        if (outputPathOrigin == OutputPathOrigin.Manual)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(defaultOutputDirectory))
        {
            if (outputPathOrigin == OutputPathOrigin.SettingsDefault)
            {
                SetOutputPath(string.Empty, OutputPathOrigin.None);
            }

            return;
        }

        string? suggestedFileName = GetSuggestedOutputFileName();
        if (string.IsNullOrWhiteSpace(suggestedFileName))
        {
            return;
        }

        SetOutputPath(
            Path.Combine(defaultOutputDirectory, suggestedFileName),
            OutputPathOrigin.SettingsDefault);
    }

    private void SetOutputPath(string value, OutputPathOrigin origin)
    {
        if (string.Equals(OutputPath, value, StringComparison.Ordinal) && outputPathOrigin == origin)
        {
            return;
        }

        isUpdatingOutputPathInternally = true;
        try
        {
            outputPathOrigin = origin;
            OutputPath = value;
        }
        finally
        {
            isUpdatingOutputPathInternally = false;
        }
    }

    private static string GetUsableDefaultOutputDirectory(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
            ? path
            : string.Empty;
    }

    private void ReportProgress(int progressRunId, FileCrypterProgress progress)
    {
        lock (progressGate)
        {
            if (!IsActiveProgressRun(progressRunId))
            {
                return;
            }

            if (progress.TotalInputBytes is > 0)
            {
                ProgressPercent = Math.Clamp(
                    progress.InputBytes * 100d / progress.TotalInputBytes.Value,
                    0,
                    100);
            }

            ProgressText = WorkflowStatusTextFormatter.FormatByteProgress(progress.InputBytes, progress.TotalInputBytes);
            FooterActionText = string.Empty;
        }
    }

    private int BeginProgressRun()
    {
        return Interlocked.Increment(ref activeProgressRunId);
    }

    private void CompleteProgressRun(int progressRunId)
    {
        Interlocked.CompareExchange(ref activeProgressRunId, 0, progressRunId);
    }

    private bool IsActiveProgressRun(int progressRunId)
    {
        return Volatile.Read(ref activeProgressRunId) == progressRunId;
    }

    private async Task CopyTextAsync(string text, string successMessage)
    {
        if (clipboardService is null || string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        try
        {
            await clipboardService.SetTextAsync(text, CancellationToken.None).ConfigureAwait(true);
            RaiseToast(WorkflowToastKind.Success, "Copied", successMessage);
        }
        catch (Exception)
        {
            ProgressText = WorkflowErrorMessageFormatter.ClipboardCopyFailureMessage;
            RaiseToast(
                WorkflowToastKind.Warning,
                "Copy failed",
                WorkflowErrorMessageFormatter.ClipboardCopyFailureMessage);
        }
    }

    private string BuildResultClipboardText()
    {
        StringBuilder builder = new();
        builder.AppendLine("Decryption complete.");
        builder.AppendLine($"Decrypted file: {ResultPath}");
        return builder.ToString().TrimEnd();
    }

    private void RaiseToast(WorkflowToastKind kind, string title, string message, string detail = "")
    {
        ToastNotificationRequested?.Invoke(this, new WorkflowToastNotification(kind, title, message, detail));
    }

    private void HandlePickerFailure(Exception exception)
    {
        _ = exception;
        string message = WorkflowErrorMessageFormatter.PickerFailureMessage;
        ProgressText = message;
        StatusText = "Ready";
        RaiseToast(WorkflowToastKind.Warning, "Picker unavailable", message);
    }

    private void ReportPathRevealFailure()
    {
        string message = WorkflowErrorMessageFormatter.PathRevealFailureMessage;
        ProgressText = message;
        RaiseToast(WorkflowToastKind.Warning, "Reveal failed", message);
    }

    private void ResetReadyFooter()
    {
        if (IsRunning)
        {
            return;
        }

        StatusText = "Ready";
        ProgressText = HasSelectedFile
            ? $"{SourcePreview!.FileName} - {SourcePreview.SizeText}"
            : "No encrypted file selected";
        FooterActionText = string.Empty;
    }

    private enum OutputPathOrigin
    {
        None,
        SettingsDefault,
        Manual,
    }
}

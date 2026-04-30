using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.Desktop.Services;
using FileCrypter.Core;
using FileCrypter.Core.Settings;
using System.Diagnostics;
using System.Text;

namespace FileCrypter.Desktop.ViewModels;

public sealed partial class EncryptViewModel : ViewModelBase, IWorkflowStatusViewModel, IWorkflowToastSource
{
    private int activeProgressRunId;
    private readonly IFileCrypterWorkflowService workflowService;
    private readonly IFilePickerService? filePickerService;
    private readonly IClipboardService? clipboardService;
    private readonly IPathRevealService pathRevealService;
    private readonly IPasswordGeneratorService passwordGeneratorService;
    private readonly object progressGate = new();
    private readonly Stopwatch runStopwatch = new();
    private string defaultOutputDirectory = string.Empty;
    private OutputPathOrigin outputPathOrigin;
    private bool isUpdatingOutputPathInternally;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartEncryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseSourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseOutputCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseGeneratedKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSourceCommand))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartEncryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearSourceCommand))]
    private string sourcePath = string.Empty;

    [ObservableProperty]
    private FileSelectionPreviewViewModel? sourcePreview;

    [ObservableProperty]
    private string outputPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartEncryptCommand))]
    private string password = string.Empty;

    [ObservableProperty]
    private bool showPassword;

    [ObservableProperty]
    private bool enableCompression;

    [ObservableProperty]
    private bool neverOverwriteExistingFiles = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseGeneratedKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearKeyFileCommand))]
    private string keyFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseGeneratedKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(GenerateKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearGeneratedKeyFileCommand))]
    private string generateKeyFilePath = string.Empty;

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string progressText = "No file selected";

    [ObservableProperty]
    private string errorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyErrorCommand))]
    private string visibleErrorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyResultCommand))]
    private string resultPath = string.Empty;

    [ObservableProperty]
    private string generatedKeyFileResultPath = string.Empty;

    [ObservableProperty]
    private bool isAdvancedOptionsExpanded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevealFooterPathCommand))]
    private string footerActionText = string.Empty;

    public EncryptViewModel(
        IFileCrypterWorkflowService workflowService,
        IFilePickerService? filePickerService = null,
        IClipboardService? clipboardService = null,
        IPathRevealService? pathRevealService = null,
        IPasswordGeneratorService? passwordGeneratorService = null)
        : this(workflowService, enableCompressionByDefault: false, filePickerService, clipboardService, pathRevealService, passwordGeneratorService)
    {
    }

    public EncryptViewModel(
        IFileCrypterWorkflowService workflowService,
        bool enableCompressionByDefault,
        IFilePickerService? filePickerService = null,
        IClipboardService? clipboardService = null,
        IPathRevealService? pathRevealService = null,
        IPasswordGeneratorService? passwordGeneratorService = null)
    {
        this.workflowService = workflowService;
        this.filePickerService = filePickerService;
        this.clipboardService = clipboardService;
        this.pathRevealService = pathRevealService ?? new NoOpPathRevealService();
        this.passwordGeneratorService = passwordGeneratorService ?? new PasswordGeneratorService();
        EnableCompression = enableCompressionByDefault;
    }

    public EncryptViewModel(
        IFileCrypterWorkflowService workflowService,
        FileCrypterSettings initialSettings,
        IFilePickerService? filePickerService = null,
        IClipboardService? clipboardService = null,
        IPathRevealService? pathRevealService = null,
        IPasswordGeneratorService? passwordGeneratorService = null)
    {
        this.workflowService = workflowService;
        this.filePickerService = filePickerService;
        this.clipboardService = clipboardService;
        this.pathRevealService = pathRevealService ?? new NoOpPathRevealService();
        this.passwordGeneratorService = passwordGeneratorService ?? new PasswordGeneratorService();
        ApplySettings(initialSettings);
    }

    public event EventHandler<WorkflowToastNotification>? ToastNotificationRequested;

    public string Title => "Encrypt a file";

    public string HeroTitle => "Encrypt a file";

    public bool HasFooterAction => !string.IsNullOrWhiteSpace(FooterActionText);

    public System.Windows.Input.ICommand FooterActionCommand => RevealFooterPathCommand;

    public bool HasError => !string.IsNullOrWhiteSpace(VisibleErrorMessage);

    public bool HasResult => !string.IsNullOrWhiteSpace(ResultPath);

    public bool HasGeneratedKeyFileResult => !string.IsNullOrWhiteSpace(GeneratedKeyFileResultPath);

    public bool ShowReadyAction => GetIsReady();

    public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SourcePath);

    public bool ShowEmptySourceState => !HasSelectedFile;

    public bool HasExistingKeyFileChoice => !string.IsNullOrWhiteSpace(KeyFilePath);

    public bool HasGeneratedKeyFileChoice => !string.IsNullOrWhiteSpace(GenerateKeyFilePath);

    public bool HasAnyKeyFileChoice => HasExistingKeyFileChoice || HasGeneratedKeyFileChoice;

    public bool CanEditExistingKeyFileChoice => !IsRunning && !HasGeneratedKeyFileChoice;

    public bool CanEditGeneratedKeyFileChoice => !IsRunning && !HasExistingKeyFileChoice;

    public bool ShowMaskedPasswordInput => !ShowPassword;

    public string PasswordVisibilityActionText => ShowPassword ? "Hide" : "Show";

    public string KeyFileChoiceStatusText => HasExistingKeyFileChoice
        ? "Using an existing key file as the optional second factor."
        : HasGeneratedKeyFileChoice
            ? "A new key file will be generated for this encryption run."
            : "No key file selected. Encryption will use only the password.";

    public int PasswordStrengthScore => GetPasswordStrengthScore(Password);

    public double PasswordStrengthPercent => PasswordStrengthScore / 4d * 100d;

    public string PasswordStrengthLabel => PasswordStrengthScore switch
    {
        <= 0 => "Enter a passphrase",
        1 => "Weak",
        2 => "Fair",
        3 => "Strong",
        _ => "Excellent",
    };

    public string PasswordStrengthDetail => string.IsNullOrWhiteSpace(Password)
        ? "Choose something memorable but uncommon."
        : $"{EstimateEntropyBits(Password)} bits of estimated entropy";

    public string FilePreviewPanelTitle => HasResult
        ? "ENCRYPTED"
        : IsRunning
            ? "CIPHER STREAM"
            : HasSelectedFile
                ? "FILE PREVIEW"
                : "PREVIEW";

    public string FilePreviewPanelBody => HasResult
        ? ResultPath
        : HasSelectedFile && SourcePreview is not null
            ? $"{SourcePreview.DisplayName} will be encrypted locally with the configured output path and protection settings."
            : "Select a file to preview the local output path and encryption parameters.";

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

        EnableCompression = settings.EnableCompressionByDefault;
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
        OnPropertyChanged(nameof(FilePreviewPanelTitle));
        OnPropertyChanged(nameof(FilePreviewPanelBody));
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
        OnPropertyChanged(nameof(PasswordStrengthScore));
        OnPropertyChanged(nameof(PasswordStrengthPercent));
        OnPropertyChanged(nameof(PasswordStrengthLabel));
        OnPropertyChanged(nameof(PasswordStrengthDetail));
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
        OnPropertyChanged(nameof(CanEditExistingKeyFileChoice));
        OnPropertyChanged(nameof(CanEditGeneratedKeyFileChoice));
        OnPropertyChanged(nameof(FilePreviewPanelTitle));
        OnPropertyChanged(nameof(FilePreviewPanelBody));
        OnPropertyChanged(nameof(ShowReadyAction));
        ClearSourceCommand.NotifyCanExecuteChanged();
    }

    partial void OnKeyFilePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(GenerateKeyFilePath))
        {
            GenerateKeyFilePath = string.Empty;
        }

        ClearVisibleError();
        OnKeyFileChoiceStateChanged();
        ResetReadyFooter();
    }

    partial void OnGenerateKeyFilePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(KeyFilePath))
        {
            KeyFilePath = string.Empty;
        }

        ClearVisibleError();
        OnKeyFileChoiceStateChanged();
        ResetReadyFooter();
    }

    partial void OnVisibleErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnResultPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(FilePreviewPanelTitle));
        OnPropertyChanged(nameof(FilePreviewPanelBody));
        OnPropertyChanged(nameof(ShowReadyAction));
    }

    partial void OnGeneratedKeyFileResultPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasGeneratedKeyFileResult));
    }

    partial void OnEnableCompressionChanged(bool value)
    {
        ResetReadyFooter();
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
    private void GenerateRandomPassword()
    {
        ApplyGeneratedPassword(passwordGeneratorService.GenerateRandomPassword());
    }

    [RelayCommand]
    private void GenerateMemorablePassphrase()
    {
        ApplyGeneratedPassword(passwordGeneratorService.GenerateMemorablePassphrase());
    }

    private void ApplyGeneratedPassword(string generatedPassword)
    {
        Password = generatedPassword;
        ShowPassword = true;
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

        string? selectedPath = await filePickerService.PickOpenFileAsync(
            "Choose a file to encrypt",
            CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            SourcePath = Path.GetFullPath(selectedPath);
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

        string? selectedPath = await filePickerService.PickSaveFileAsync(
            "Choose encrypted output file",
            GetSuggestedOutputFileName(),
            CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            OutputPath = selectedPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowseExistingKeyFile))]
    private async Task BrowseKeyFileAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        string? selectedPath = await filePickerService.PickOpenFileAsync(
            "Choose an existing key file",
            CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            KeyFilePath = selectedPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowseGeneratedKeyFile))]
    private async Task BrowseGeneratedKeyFileAsync()
    {
        await ChooseGeneratedKeyFilePathAsync().ConfigureAwait(true);
    }

    [RelayCommand(CanExecute = nameof(CanBrowseGeneratedKeyFile))]
    private async Task GenerateKeyFileAsync()
    {
        await ChooseGeneratedKeyFilePathAsync().ConfigureAwait(true);
    }

    private async Task ChooseGeneratedKeyFilePathAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        string? selectedPath = await filePickerService.PickSaveFileAsync(
            "Choose where to save a new key file",
            "filecrypter.key",
            CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            GenerateKeyFilePath = selectedPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearExistingKeyFile))]
    private void ClearKeyFile()
    {
        KeyFilePath = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanClearGeneratedKeyFile))]
    private void ClearGeneratedKeyFile()
    {
        GenerateKeyFilePath = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanStartEncrypt))]
    private async Task StartEncryptAsync()
    {
        int progressRunId = BeginProgressRun();
        IsRunning = true;
        runStopwatch.Restart();
        ErrorMessage = string.Empty;
        ResultPath = string.Empty;
        GeneratedKeyFileResultPath = string.Empty;
        ProgressPercent = 0;
        FooterActionText = string.Empty;
        ProgressText = "Preparing source file...";
        StatusText = "Encrypting";

        try
        {
            EncryptFileRequest request = new(
                SourcePath,
                string.IsNullOrWhiteSpace(OutputPath) ? null : OutputPath,
                Password,
                EnableCompression,
                NeverOverwriteExistingFiles,
                string.IsNullOrWhiteSpace(KeyFilePath) ? null : KeyFilePath,
                string.IsNullOrWhiteSpace(GenerateKeyFilePath) ? null : GenerateKeyFilePath);

            Progress<FileCrypterProgress> progress = new(value => ReportProgress(progressRunId, value));
            EncryptFileResult result = await workflowService.EncryptFileAsync(
                request,
                progress,
                CancellationToken.None);

            lock (progressGate)
            {
                CompleteProgressRun(progressRunId);
                runStopwatch.Stop();
                ResultPath = result.OutputPath;
                GeneratedKeyFileResultPath = result.GeneratedKeyFilePath ?? string.Empty;
                Password = string.Empty;
                ProgressPercent = 100;
                ProgressText = "Saved to";
                StatusText = $"Encrypted in {WorkflowStatusTextFormatter.FormatElapsed(runStopwatch.Elapsed)}";
                FooterActionText = result.OutputPath;
            }

            RaiseToast(
                WorkflowToastKind.Success,
                "Encryption complete",
                "Encrypted file saved.",
                result.OutputPath);
        }
        catch (Exception exception)
        {
            lock (progressGate)
            {
                CompleteProgressRun(progressRunId);
                runStopwatch.Stop();
                string message = GetTroubleshootingMessage(exception);
                ErrorMessage = message;
                VisibleErrorMessage = message;
                ProgressText = WorkflowStatusTextFormatter.SummarizeStatusDetail(message);
                StatusText = "Encryption failed";
                FooterActionText = string.Empty;
            }
        }
        finally
        {
            IsRunning = false;
        }
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
    private Task RevealFooterPathAsync()
    {
        return pathRevealService.TryRevealPathAsync(FooterActionText, CancellationToken.None);
    }

    private bool CanBrowse()
    {
        return !IsRunning;
    }

    private bool CanClearSource()
    {
        return !IsRunning && HasSelectedFile;
    }

    private bool CanBrowseExistingKeyFile()
    {
        return !IsRunning && !HasGeneratedKeyFileChoice;
    }

    private bool CanBrowseGeneratedKeyFile()
    {
        return !IsRunning && !HasExistingKeyFileChoice;
    }

    private bool CanClearExistingKeyFile()
    {
        return !IsRunning && HasExistingKeyFileChoice;
    }

    private bool CanClearGeneratedKeyFile()
    {
        return !IsRunning && HasGeneratedKeyFileChoice;
    }

    private bool CanStartEncrypt()
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
        return string.IsNullOrWhiteSpace(SourcePath)
            ? null
            : Path.GetFileName(SourcePath) + ".encrypted";
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
        catch (Exception exception)
        {
            ProgressText = $"Clipboard copy failed: {exception.Message}";
        }
    }

    private string BuildResultClipboardText()
    {
        StringBuilder builder = new();
        builder.AppendLine("Encryption complete.");
        builder.AppendLine($"Encrypted file: {ResultPath}");

        if (HasGeneratedKeyFileResult)
        {
            builder.AppendLine($"Generated key file: {GeneratedKeyFileResultPath}");
        }

        return builder.ToString().TrimEnd();
    }

    private void RaiseToast(WorkflowToastKind kind, string title, string message, string detail = "")
    {
        ToastNotificationRequested?.Invoke(this, new WorkflowToastNotification(kind, title, message, detail));
    }

    private void OnKeyFileChoiceStateChanged()
    {
        OnPropertyChanged(nameof(HasExistingKeyFileChoice));
        OnPropertyChanged(nameof(HasGeneratedKeyFileChoice));
        OnPropertyChanged(nameof(HasAnyKeyFileChoice));
        OnPropertyChanged(nameof(CanEditExistingKeyFileChoice));
        OnPropertyChanged(nameof(CanEditGeneratedKeyFileChoice));
        OnPropertyChanged(nameof(KeyFileChoiceStatusText));
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
            : "No file selected";
        FooterActionText = string.Empty;
    }

    private static string GetTroubleshootingMessage(Exception exception)
    {
        return WorkflowErrorMessageFormatter.GetTroubleshootingMessage(exception);
    }

    private static int GetPasswordStrengthScore(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return 0;
        }

        int score = 0;
        if (password.Length >= 8)
        {
            score++;
        }

        if (password.Length >= 12)
        {
            score++;
        }

        if (password.Any(char.IsUpper))
        {
            score++;
        }

        if (password.Any(static character => !char.IsLetterOrDigit(character)))
        {
            score++;
        }

        if (password.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Length >= 4)
        {
            score++;
        }

        return Math.Clamp(score, 0, 4);
    }

    private static int EstimateEntropyBits(string password)
    {
        if (string.IsNullOrWhiteSpace(password))
        {
            return 0;
        }

        int estimate = password.Length * 4;
        if (password.Length >= 12)
        {
            estimate += 10;
        }

        if (password.Any(char.IsUpper))
        {
            estimate += 6;
        }

        if (password.Any(static character => !char.IsLetterOrDigit(character)))
        {
            estimate += 6;
        }

        return estimate;
    }

    private enum OutputPathOrigin
    {
        None,
        SettingsDefault,
        Manual,
    }
}

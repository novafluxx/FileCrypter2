using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;
using FileCrypter.Core;
using FileCrypter.Core.Settings;
using System.Text;
using CoreFileCrypter = FileCrypter.Core.FileCrypter;

namespace FileCrypter.App.ViewModels;

public sealed partial class BatchViewModel : ViewModelBase, IWorkflowStatusViewModel, IWorkflowToastSource
{
    private int activeProgressRunId;
    private readonly IFileCrypterWorkflowService workflowService;
    private readonly IFilePickerService? filePickerService;
    private readonly IClipboardService? clipboardService;
    private readonly IPathRevealService pathRevealService;
    private readonly IPasswordGeneratorService passwordGeneratorService;
    private readonly Stopwatch runStopwatch = new();
    private readonly StringComparer pathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;
    private OutputDirectoryOrigin outputDirectoryOrigin;
    private bool isUpdatingOutputDirectoryInternally;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBatchCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseOutputDirectoryCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearFilesCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearKeyFileCommand))]
    private bool isRunning;

    [ObservableProperty]
    private bool encryptMode = true;

    [ObservableProperty]
    private bool archiveMode;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RemoveSelectedFileCommand))]
    private string? selectedSourcePath;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBatchCommand))]
    private string outputDirectory = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartBatchCommand))]
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
    private string archiveName = string.Empty;

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string progressText = "No batch files selected";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyErrorCommand))]
    private string errorMessage = "No batch files selected. Add files to encrypt.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyErrorCommand))]
    private string visibleErrorMessage = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyResultsCommand))]
    private string resultSummary = string.Empty;

    [ObservableProperty]
    private bool isAdvancedOptionsExpanded;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RevealFooterPathCommand))]
    private string footerActionText = string.Empty;

    public BatchViewModel(
        IFileCrypterWorkflowService workflowService,
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

        SourcePaths.CollectionChanged += OnSourcePathsChanged;
        Results.CollectionChanged += OnResultsChanged;
    }

    public BatchViewModel(
        IFileCrypterWorkflowService workflowService,
        FileCrypterSettings initialSettings,
        IFilePickerService? filePickerService = null,
        IClipboardService? clipboardService = null,
        IPathRevealService? pathRevealService = null,
        IPasswordGeneratorService? passwordGeneratorService = null)
        : this(workflowService, filePickerService, clipboardService, pathRevealService, passwordGeneratorService)
    {
        ApplySettings(initialSettings);
    }

    public event EventHandler<WorkflowToastNotification>? ToastNotificationRequested;

    public string Title => "Batch workflows";

    public ObservableCollection<string> SourcePaths { get; } = [];

    public ObservableCollection<FileSelectionPreviewViewModel> SourceFilePreviews { get; } = [];

    public ObservableCollection<BatchResultItemViewModel> Results { get; } = [];

    public bool IsDecryptMode
    {
        get => !EncryptMode;
        set
        {
            if (value)
            {
                EncryptMode = false;
            }
        }
    }

    public bool IndividualFilesMode
    {
        get => !ArchiveMode;
        set
        {
            if (value)
            {
                ArchiveMode = false;
            }
        }
    }

    public bool HasSelectedFiles => SourcePaths.Count > 0;

    public bool ShowEmptySourceState => !HasSelectedFiles;

    public bool HasError => !string.IsNullOrWhiteSpace(VisibleErrorMessage);

    public bool HasResults => Results.Count > 0;

    public bool HasResultSummary => !string.IsNullOrWhiteSpace(ResultSummary);

    public bool HasFailures => Results.Any(item => !item.Succeeded);

    public bool HasKeyFileChoice => !string.IsNullOrWhiteSpace(KeyFilePath);

    public bool HasFooterAction => !string.IsNullOrWhiteSpace(FooterActionText);

    public System.Windows.Input.ICommand FooterActionCommand => RevealFooterPathCommand;

    public bool ShowMaskedPasswordInput => !ShowPassword;

    public string PasswordVisibilityActionText => ShowPassword ? "Hide" : "Show";

    public int PasswordStrengthScore => GetPasswordStrengthScore(Password);

    public double PasswordStrengthPercent => PasswordStrengthScore / 4d * 100d;

    public string PasswordStrengthLabel => PasswordStrengthScore switch
    {
        0 => "Enter a password",
        1 => "Weak",
        2 => "Fair",
        3 => "Strong",
        _ => "Excellent",
    };

    public string PasswordStrengthDetail => string.IsNullOrWhiteSpace(Password)
        ? "Choose something memorable but uncommon."
        : $"{EstimateEntropyBits(Password)} bits of estimated entropy";

    public bool ShowArchiveNameEditor => ArchiveMode && EncryptMode;

    public string QueueInspectorTitle => HasResults
        ? "LAST RUN"
        : HasSelectedFiles
            ? "QUEUED ITEMS"
            : "QUEUE PREVIEW";

    public string QueueInspectorBody => HasResults
        ? ResultSummary
        : HasSelectedFiles
            ? FilesSummaryText
            : "Build a batch, choose an output folder, then run the queue when everything looks right.";

    public string ModeTitle => (ArchiveMode, EncryptMode) switch
    {
        (false, true) => "Encrypt many files at once",
        (false, false) => "Decrypt many files at once",
        (true, true) => "Create one encrypted archive",
        _ => "Decrypt and extract one encrypted archive",
    };

    public string WorkflowChooserDescription => "Pick what you have first, then choose what FileCrypter should do with it. All four combinations stay available, including archive extraction.";

    public string IndividualFilesOptionDescription => "Keep every selected file separate so the run produces one output per file.";

    public string ArchiveModeOptionDescription => "Switch to one encrypted archive when many files belong together, or when one archive needs to be extracted.";

    public string EncryptActionDescription => ArchiveMode
        ? "Create one encrypted archive from the current file list."
        : "Encrypt each selected file into its own output.";

    public string DecryptActionDescription => ArchiveMode
        ? "Decrypt and extract one encrypted archive into the chosen folder."
        : "Decrypt each selected file separately into the chosen folder.";

    public string WorkflowKindDescription => (ArchiveMode, EncryptMode) switch
    {
        (false, true) => "Current selection: individual-file encryption. Every chosen file becomes its own encrypted output in the target folder.",
        (false, false) => "Current selection: individual-file decryption. Every chosen encrypted file is restored separately, and per-file failures stay isolated.",
        (true, true) => "Current selection: archive encryption. Many selected files are bundled into one compressed .tar.zst.encrypted archive.",
        _ => "Current selection: archive extraction. Choose exactly one encrypted archive and FileCrypter will decrypt and extract it into the target folder.",
    };

    public string SourceHeading => (ArchiveMode, EncryptMode) switch
    {
        (false, true) => "FILES TO ENCRYPT",
        (false, false) => "FILES TO DECRYPT",
        (true, true) => "FILES TO INCLUDE",
        _ => "ARCHIVE TO EXTRACT",
    };

    public string OutputHeading => (ArchiveMode, EncryptMode) switch
    {
        (false, true) => "OUTPUT DIRECTORY",
        (false, false) => "DECRYPTED OUTPUT DIRECTORY",
        (true, true) => "ARCHIVE OUTPUT DIRECTORY",
        _ => "EXTRACTION DIRECTORY",
    };

    public string OutputDescription => (ArchiveMode, EncryptMode) switch
    {
        (false, true) => "Every input file writes one .encrypted output into this existing folder.",
        (false, false) => "Every input file writes one decrypted output into this existing folder.",
        (true, true) => "FileCrypter writes one .tar.zst.encrypted archive into this existing folder.",
        _ => "FileCrypter decrypts the selected archive and extracts each file into this existing folder.",
    };

    public string ArchiveNameStatusText => string.IsNullOrWhiteSpace(ArchiveName)
        ? "Leave this blank to generate a timestamped .tar.zst.encrypted archive name automatically."
        : "FileCrypter adds the .tar.zst.encrypted suffix automatically and keeps the archive name platform-safe.";

    public string FilesSummaryText
    {
        get
        {
            if (!HasSelectedFiles)
            {
                return (ArchiveMode, EncryptMode) switch
                {
                    (false, true) => "No files selected yet. Add one or more files to encrypt.",
                    (false, false) => "No encrypted files selected yet. Add one or more files to decrypt.",
                    (true, true) => "No files selected yet. Add one or more files to bundle into one encrypted archive.",
                    _ => "No archive selected yet. Choose one encrypted archive to extract.",
                };
            }

            if (ArchiveMode && !EncryptMode)
            {
                return SourcePaths.Count == 1
                    ? $"Ready to extract archive: {Path.GetFileName(SourcePaths[0])}"
                    : $"Choose exactly one encrypted archive file to extract. {SourcePaths.Count} file(s) are currently selected.";
            }

            return ArchiveMode
                ? $"{SourcePaths.Count} file(s) selected for one encrypted archive."
                : $"{SourcePaths.Count} file(s) selected. FileCrypter processes up to {CoreFileCrypter.MaximumBatchFileCount} files per batch.";
        }
    }

    public string KeyFileChoiceStatusText => ArchiveMode
        ? EncryptMode
            ? HasKeyFileChoice
                ? "Using the selected key file as the optional second factor for the encrypted archive."
                : "No key file selected. Archive encryption will use only the password."
            : HasKeyFileChoice
                ? "Using the selected key file as the optional second factor for archive extraction."
                : "No key file selected. Archive decryption will use only the password unless the archive requires one."
        : HasKeyFileChoice
            ? EncryptMode
                ? "Using the selected key file as the optional second factor for every encrypted file in this batch."
                : "Using the selected key file as the optional second factor for every decryption attempt in this batch."
            : EncryptMode
                ? "No key file selected. Batch encryption will use only the password."
                : "No key file selected. Batch decryption will use only the password unless a file requires one.";

    public string ActionButtonText => (ArchiveMode, EncryptMode) switch
    {
        (false, true) => "Encrypt selected files",
        (false, false) => "Decrypt selected files",
        (true, true) => "Create encrypted archive",
        _ => "Decrypt and extract archive",
    };

    public string OutputFolderPickerTitle => ArchiveMode && EncryptMode
        ? "Choose archive output directory"
        : ArchiveMode
            ? "Choose archive extraction directory"
            : "Choose batch output directory";

    public bool ApplyDroppedSourcePaths(IEnumerable<string> droppedPaths)
    {
        ArgumentNullException.ThrowIfNull(droppedPaths);

        if (IsRunning)
        {
            return false;
        }

        string[] normalizedPaths = droppedPaths
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .ToArray();

        if (normalizedPaths.Length == 0)
        {
            return false;
        }

        if (ArchiveMode && !EncryptMode)
        {
            SetSourcePaths(normalizedPaths);
        }
        else
        {
            AddSourcePaths(normalizedPaths);
        }

        return true;
    }

    partial void OnEncryptModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDecryptMode));
        OnWorkflowModeChanged();
    }

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(QueueInspectorTitle));
        OnPropertyChanged(nameof(QueueInspectorBody));
        foreach (FileSelectionPreviewViewModel preview in SourceFilePreviews)
        {
            preview.NotifyRemoveCommandChanged();
        }
    }

    partial void OnArchiveModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IndividualFilesMode));
        OnWorkflowModeChanged();
    }

    partial void OnKeyFilePathChanged(string value)
    {
        RefreshIdleValidationState();
        OnPropertyChanged(nameof(HasKeyFileChoice));
        OnPropertyChanged(nameof(KeyFileChoiceStatusText));
    }

    partial void OnOutputDirectoryChanged(string value)
    {
        if (!isUpdatingOutputDirectoryInternally)
        {
            outputDirectoryOrigin = string.IsNullOrWhiteSpace(value)
                ? OutputDirectoryOrigin.None
                : OutputDirectoryOrigin.Manual;
        }

        RefreshIdleValidationState();
    }

    partial void OnPasswordChanged(string value)
    {
        OnPropertyChanged(nameof(PasswordStrengthScore));
        OnPropertyChanged(nameof(PasswordStrengthPercent));
        OnPropertyChanged(nameof(PasswordStrengthLabel));
        OnPropertyChanged(nameof(PasswordStrengthDetail));
        RefreshIdleValidationState();
    }

    partial void OnShowPasswordChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowMaskedPasswordInput));
        OnPropertyChanged(nameof(PasswordVisibilityActionText));
    }

    partial void OnArchiveNameChanged(string value)
    {
        OnPropertyChanged(nameof(ArchiveNameStatusText));
        RefreshIdleValidationState();
    }

    partial void OnVisibleErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnResultSummaryChanged(string value)
    {
        OnPropertyChanged(nameof(HasResultSummary));
        OnPropertyChanged(nameof(QueueInspectorTitle));
        OnPropertyChanged(nameof(QueueInspectorBody));
    }

    partial void OnNeverOverwriteExistingFilesChanged(bool value)
    {
        RefreshIdleValidationState();
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

    [RelayCommand(CanExecute = nameof(CanBrowseFiles))]
    private async Task BrowseFilesAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        if (ArchiveMode && !EncryptMode)
        {
            string? selectedPath = await filePickerService.PickOpenFileAsync(
                "Choose an encrypted archive to extract",
                CancellationToken.None);
            if (!string.IsNullOrWhiteSpace(selectedPath))
            {
                SetSourcePaths([selectedPath]);
            }

            return;
        }

        IReadOnlyList<string> selectedPaths = await filePickerService.PickOpenFilesAsync(
            ArchiveMode ? "Choose files to include in the encrypted archive" : EncryptMode ? "Choose files to encrypt" : "Choose files to decrypt",
            CancellationToken.None);

        AddSourcePaths(selectedPaths);
    }

    [RelayCommand(CanExecute = nameof(CanBrowseOutputDirectory))]
    private async Task BrowseOutputDirectoryAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        string? selectedPath = await filePickerService.PickOpenFolderAsync(
            OutputFolderPickerTitle,
            CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            SetOutputDirectory(Path.GetFullPath(selectedPath), OutputDirectoryOrigin.Manual);
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowseKeyFile))]
    private async Task BrowseKeyFileAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        string? selectedPath = await filePickerService.PickOpenFileAsync(
            ArchiveMode
                ? EncryptMode
                    ? "Choose an existing key file for the archive"
                    : "Choose the matching key file for the archive"
                : EncryptMode
                    ? "Choose an existing key file for the batch"
                    : "Choose the matching key file for the batch",
            CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            KeyFilePath = selectedPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRemoveSelectedFile))]
    private void RemoveSelectedFile()
    {
        if (string.IsNullOrWhiteSpace(SelectedSourcePath))
        {
            return;
        }

        string? existingPath = SourcePaths.FirstOrDefault(path => pathComparer.Equals(path, SelectedSourcePath));
        if (existingPath is not null)
        {
            SourcePaths.Remove(existingPath);
        }
    }

    [RelayCommand(CanExecute = nameof(CanClearFiles))]
    private void ClearFiles()
    {
        SourcePaths.Clear();
        ProgressPercent = 0;
        ClearResults();
        ResetIdleStateForMode();
    }

    [RelayCommand(CanExecute = nameof(CanClearKeyFile))]
    private void ClearKeyFile()
    {
        KeyFilePath = string.Empty;
    }

    [RelayCommand(CanExecute = nameof(CanStartBatch))]
    private async Task StartBatchAsync()
    {
        int progressRunId = BeginProgressRun();
        IsRunning = true;
        runStopwatch.Restart();
        ErrorMessage = string.Empty;
        ProgressPercent = 0;
        FooterActionText = string.Empty;
        StatusText = GetRunningStatusText();
        ProgressText = GetStartingProgressText();
        ClearResults();

        try
        {
            if (ArchiveMode)
            {
                if (EncryptMode)
                {
                    await RunArchiveEncryptAsync(progressRunId);
                }
                else
                {
                    await RunArchiveDecryptAsync(progressRunId);
                }
            }
            else
            {
                await RunIndividualBatchAsync(progressRunId);
            }
        }
        catch (Exception exception)
        {
            runStopwatch.Stop();
            string message = WorkflowErrorMessageFormatter.GetTroubleshootingMessage(exception);
            ErrorMessage = message;
            VisibleErrorMessage = message;
            ProgressText = WorkflowStatusTextFormatter.SummarizeStatusDetail(message);
            StatusText = GetFailureStatusText();
            FooterActionText = string.Empty;
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

    [RelayCommand(CanExecute = nameof(CanCopyResults))]
    private Task CopyResultsAsync()
    {
        return CopyTextAsync(BuildResultsClipboardText(), "Copied batch results.");
    }

    [RelayCommand(CanExecute = nameof(CanRevealFooterPath))]
    private Task RevealFooterPathAsync()
    {
        return pathRevealService.TryRevealPathAsync(FooterActionText, CancellationToken.None);
    }

    private bool CanBrowseFiles()
    {
        return !IsRunning && filePickerService is not null;
    }

    private bool CanBrowseOutputDirectory()
    {
        return !IsRunning && filePickerService is not null;
    }

    private bool CanBrowseKeyFile()
    {
        return !IsRunning && filePickerService is not null;
    }

    private bool CanRemoveSelectedFile()
    {
        return !IsRunning && !string.IsNullOrWhiteSpace(SelectedSourcePath);
    }

    private bool CanClearFiles()
    {
        return !IsRunning && HasSelectedFiles;
    }

    private bool CanClearKeyFile()
    {
        return !IsRunning && HasKeyFileChoice;
    }

    private bool CanStartBatch()
    {
        return !IsRunning &&
            HasValidSourceSelection() &&
            HasValidOutputDirectory() &&
            !string.IsNullOrWhiteSpace(Password) &&
            HasValidArchiveName();
    }

    private bool CanCopyError()
    {
        return clipboardService is not null && HasError;
    }

    private bool CanCopyResults()
    {
        return clipboardService is not null && HasResults;
    }

    private bool CanRevealFooterPath()
    {
        return HasFooterAction;
    }

    private void OnSourcePathsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (!IsRunning && HasResults)
        {
            ClearResults();
        }

        if (!string.IsNullOrWhiteSpace(SelectedSourcePath) &&
            !SourcePaths.Any(path => pathComparer.Equals(path, SelectedSourcePath)))
        {
            SelectedSourcePath = null;
        }

        RefreshSourceFilePreviews();
        OnPropertyChanged(nameof(HasSelectedFiles));
        OnPropertyChanged(nameof(ShowEmptySourceState));
        OnPropertyChanged(nameof(FilesSummaryText));
        OnPropertyChanged(nameof(QueueInspectorTitle));
        OnPropertyChanged(nameof(QueueInspectorBody));
        StartBatchCommand.NotifyCanExecuteChanged();
        RemoveSelectedFileCommand.NotifyCanExecuteChanged();
        ClearFilesCommand.NotifyCanExecuteChanged();

        ResetIdleStateForMode();
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasFailures));
        OnPropertyChanged(nameof(QueueInspectorTitle));
        OnPropertyChanged(nameof(QueueInspectorBody));
        CopyResultsCommand.NotifyCanExecuteChanged();
    }

    private void OnWorkflowModeChanged()
    {
        OnPropertyChanged(nameof(ModeTitle));
        OnPropertyChanged(nameof(WorkflowChooserDescription));
        OnPropertyChanged(nameof(IndividualFilesOptionDescription));
        OnPropertyChanged(nameof(ArchiveModeOptionDescription));
        OnPropertyChanged(nameof(EncryptActionDescription));
        OnPropertyChanged(nameof(DecryptActionDescription));
        OnPropertyChanged(nameof(WorkflowKindDescription));
        OnPropertyChanged(nameof(SourceHeading));
        OnPropertyChanged(nameof(OutputHeading));
        OnPropertyChanged(nameof(OutputDescription));
        OnPropertyChanged(nameof(ShowArchiveNameEditor));
        OnPropertyChanged(nameof(ArchiveNameStatusText));
        OnPropertyChanged(nameof(FilesSummaryText));
        OnPropertyChanged(nameof(KeyFileChoiceStatusText));
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(OutputFolderPickerTitle));
        OnPropertyChanged(nameof(QueueInspectorTitle));
        OnPropertyChanged(nameof(QueueInspectorBody));

        StartBatchCommand.NotifyCanExecuteChanged();
        ClearResults();
        ResetIdleStateForMode();
    }

    public void ApplySettings(FileCrypterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        NeverOverwriteExistingFiles = settings.NeverOverwriteExistingFilesByDefault;
        string defaultDirectory = GetUsableDefaultOutputDirectory(settings.DefaultOutputDirectory);

        if (string.IsNullOrWhiteSpace(defaultDirectory))
        {
            if (outputDirectoryOrigin == OutputDirectoryOrigin.SettingsDefault)
            {
                SetOutputDirectory(string.Empty, OutputDirectoryOrigin.None);
            }
        }
        else if (outputDirectoryOrigin is OutputDirectoryOrigin.None or OutputDirectoryOrigin.SettingsDefault)
        {
            SetOutputDirectory(defaultDirectory, OutputDirectoryOrigin.SettingsDefault);
        }

        RefreshIdleValidationState();
    }

    private async Task RunIndividualBatchAsync(int progressRunId)
    {
        BatchTransformRequest request = new(
            SourcePaths.ToArray(),
            OutputDirectory,
            Password,
            NeverOverwriteExistingFiles,
            string.IsNullOrWhiteSpace(KeyFilePath) ? null : KeyFilePath);

        Progress<BatchOperationProgress> progress = new(value => ReportBatchProgress(progressRunId, value));
        BatchTransformResult result = EncryptMode
            ? await workflowService.EncryptFilesAsync(request, progress, CancellationToken.None)
            : await workflowService.DecryptFilesAsync(request, progress, CancellationToken.None);

        CompleteProgressRun(progressRunId);
        runStopwatch.Stop();
        ApplyBatchResults(result);
        Password = string.Empty;
        ProgressPercent = 100;
        StatusText = result.Succeeded
            ? EncryptMode
                ? $"Batch encrypted in {WorkflowStatusTextFormatter.FormatElapsed(runStopwatch.Elapsed)}"
                : $"Batch decrypted in {WorkflowStatusTextFormatter.FormatElapsed(runStopwatch.Elapsed)}"
            : EncryptMode
                ? "Batch encryption completed with failures"
                : "Batch decryption completed with failures";
        ProgressText = result.Succeeded
            ? "Saved in"
            : $"{result.SucceededCount} succeeded, {result.FailedCount} failed - Output folder";
        FooterActionText = OutputDirectory;
        ResultSummary = result.Succeeded
            ? EncryptMode
                ? $"Batch encryption complete. {result.SucceededCount} file(s) succeeded."
                : $"Batch decryption complete. {result.SucceededCount} file(s) succeeded."
            : EncryptMode
                ? $"Batch encryption finished with {result.FailedCount} failure(s) and {result.SucceededCount} success(es)."
                : $"Batch decryption finished with {result.FailedCount} failure(s) and {result.SucceededCount} success(es).";
        RaiseToast(
            result.Succeeded ? WorkflowToastKind.Success : WorkflowToastKind.Warning,
            result.Succeeded ? "Batch complete" : "Batch completed with failures",
            ResultSummary,
            OutputDirectory);
    }

    private async Task RunArchiveEncryptAsync(int progressRunId)
    {
        ArchiveEncryptRequest request = new(
            SourcePaths.ToArray(),
            OutputDirectory,
            Password,
            NeverOverwriteExistingFiles,
            string.IsNullOrWhiteSpace(KeyFilePath) ? null : KeyFilePath,
            string.IsNullOrWhiteSpace(ArchiveName) ? null : ArchiveName);

        Progress<FileCrypterProgress> progress = new(value => ReportArchiveProgress(progressRunId, value));
        ArchiveEncryptResult result = await workflowService.EncryptArchiveAsync(
            request,
            progress,
            CancellationToken.None);

        CompleteProgressRun(progressRunId);
        runStopwatch.Stop();
        Password = string.Empty;
        Results.Add(new BatchResultItemViewModel(
            succeeded: true,
            statusLabel: "Created",
            statusBrush: "#4f9a61",
            titleText: Path.GetFileName(result.OutputPath),
            primaryText: result.OutputPath,
            detailLabel: "CONTENTS",
            detailText: $"{SourcePaths.Count} selected file(s) bundled into this encrypted archive.",
            detailBrush: "#4f9a61",
            secondaryText: string.Empty));
        ProgressPercent = 100;
        StatusText = $"Archive created in {WorkflowStatusTextFormatter.FormatElapsed(runStopwatch.Elapsed)}";
        ProgressText = "Saved to";
        FooterActionText = result.OutputPath;
        ResultSummary = $"Archive encryption complete. {SourcePaths.Count} file(s) bundled into one encrypted archive.";
        RaiseToast(
            WorkflowToastKind.Success,
            "Archive created",
            ResultSummary,
            result.OutputPath);
    }

    private async Task RunArchiveDecryptAsync(int progressRunId)
    {
        ArchiveDecryptRequest request = new(
            SourcePaths.Single(),
            OutputDirectory,
            Password,
            NeverOverwriteExistingFiles,
            string.IsNullOrWhiteSpace(KeyFilePath) ? null : KeyFilePath);

        Progress<FileCrypterProgress> progress = new(value => ReportArchiveProgress(progressRunId, value));
        ArchiveDecryptResult result = await workflowService.DecryptArchiveAsync(
            request,
            progress,
            CancellationToken.None);

        CompleteProgressRun(progressRunId);
        runStopwatch.Stop();
        Password = string.Empty;
        foreach (string outputPath in result.OutputPaths)
        {
            Results.Add(new BatchResultItemViewModel(
                succeeded: true,
                statusLabel: "Extracted",
                statusBrush: "#4f9a61",
                titleText: Path.GetFileName(outputPath),
                primaryText: outputPath,
                detailLabel: "SOURCE ARCHIVE",
                detailText: request.SourcePath,
                detailBrush: "#4f9a61",
                secondaryText: string.Empty));
        }

        ProgressPercent = 100;
        StatusText = $"Archive extracted in {WorkflowStatusTextFormatter.FormatElapsed(runStopwatch.Elapsed)}";
        ProgressText = $"Extracted {result.OutputPaths.Count} file(s) to";
        FooterActionText = OutputDirectory;
        ResultSummary = $"Archive extraction complete. {result.OutputPaths.Count} file(s) extracted.";
        RaiseToast(
            WorkflowToastKind.Success,
            "Archive extracted",
            ResultSummary,
            OutputDirectory);
    }

    private void AddSourcePaths(IEnumerable<string> selectedPaths)
    {
        foreach (string selectedPath in selectedPaths)
        {
            if (string.IsNullOrWhiteSpace(selectedPath))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(selectedPath);
            if (SourcePaths.Any(existingPath => pathComparer.Equals(existingPath, fullPath)))
            {
                continue;
            }

            TrySetDefaultOutputDirectory(fullPath);
            SourcePaths.Add(fullPath);
        }
    }

    private void SetSourcePaths(IEnumerable<string> selectedPaths)
    {
        SourcePaths.Clear();
        AddSourcePaths(selectedPaths);
    }

    private void RefreshSourceFilePreviews()
    {
        SourceFilePreviews.Clear();

        foreach (string sourcePath in SourcePaths)
        {
            string pathForRemoval = sourcePath;
            SourceFilePreviews.Add(new FileSelectionPreviewViewModel(
                pathForRemoval,
                removeAction: () => RemoveSourcePath(pathForRemoval),
                canRemove: () => !IsRunning));
        }
    }

    private void RemoveSourcePath(string sourcePath)
    {
        string? existingPath = SourcePaths.FirstOrDefault(path => pathComparer.Equals(path, sourcePath));
        if (existingPath is not null)
        {
            SourcePaths.Remove(existingPath);
        }
    }

    private void ClearResults()
    {
        ResultSummary = string.Empty;
        Results.Clear();
    }

    private void ApplyBatchResults(BatchTransformResult result)
    {
        foreach (BatchTransformItemResult item in result.Items)
        {
            if (item.Succeeded)
            {
                string outputPath = item.OutputPath ?? item.RequestedOutputPath;
                Results.Add(new BatchResultItemViewModel(
                    succeeded: true,
                    statusLabel: "Succeeded",
                    statusBrush: "#4f9a61",
                    titleText: Path.GetFileName(item.InputPath),
                    primaryText: item.InputPath,
                    detailLabel: "OUTPUT",
                    detailText: outputPath,
                    detailBrush: "#4f9a61",
                    secondaryText: string.Empty));
            }
            else
            {
                Results.Add(new BatchResultItemViewModel(
                    succeeded: false,
                    statusLabel: "Failed",
                    statusBrush: "#c78a2f",
                    titleText: Path.GetFileName(item.InputPath),
                    primaryText: item.InputPath,
                    detailLabel: "ISSUE",
                    detailText: WorkflowErrorMessageFormatter.GetTroubleshootingMessage(item.Error!),
                    detailBrush: "#c78a2f",
                    secondaryText: $"Requested output: {item.RequestedOutputPath}"));
            }
        }
    }

    private void ReportBatchProgress(int progressRunId, BatchOperationProgress progress)
    {
        if (!IsActiveProgressRun(progressRunId))
        {
            return;
        }

        ProgressPercent = progress.Percent;

        if (progress.TotalFiles == 0)
        {
            ProgressText = "No batch files selected";
            return;
        }

        if (string.IsNullOrWhiteSpace(progress.CurrentInputPath))
        {
            ProgressText = EncryptMode
                ? $"Encrypting {progress.TotalFiles} selected files..."
                : $"Decrypting {progress.TotalFiles} selected files...";
            return;
        }

        string currentFileName = Path.GetFileName(progress.CurrentInputPath);
        int currentFileNumber = Math.Min(progress.CompletedFiles + 1, progress.TotalFiles);

        ProgressText = progress.CurrentTotalInputBytes is > 0
            ? $"File {currentFileNumber}/{progress.TotalFiles} - {currentFileName} - {WorkflowStatusTextFormatter.FormatByteProgress(progress.CurrentInputBytes, progress.CurrentTotalInputBytes)}"
            : $"File {currentFileNumber}/{progress.TotalFiles} - {currentFileName}";
        FooterActionText = string.Empty;
    }

    private void ReportArchiveProgress(int progressRunId, FileCrypterProgress progress)
    {
        if (!IsActiveProgressRun(progressRunId))
        {
            return;
        }

        if (progress.TotalInputBytes is > 0)
        {
            ProgressPercent = Math.Clamp(progress.InputBytes * 100d / progress.TotalInputBytes.Value, 0, 100);
        }

        string phaseText = progress.Phase switch
        {
            FileCrypterProgressPhases.CreatingArchive => "Creating archive",
            FileCrypterProgressPhases.EncryptingArchive => "Encrypting archive",
            FileCrypterProgressPhases.DecryptingArchive => "Decrypting archive",
            FileCrypterProgressPhases.ExtractingArchive => "Extracting archive",
            _ => ArchiveMode && EncryptMode ? "Working on archive" : "Working on extraction",
        };

        ProgressText = progress.TotalInputBytes is > 0
            ? $"{phaseText} - {WorkflowStatusTextFormatter.FormatByteProgress(progress.InputBytes, progress.TotalInputBytes)}"
            : phaseText;
        FooterActionText = string.Empty;
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

    private void ResetIdleStateForMode()
    {
        if (IsRunning || HasResults)
        {
            return;
        }

        VisibleErrorMessage = string.Empty;
        ErrorMessage = GetBlockingValidationMessage();
        StatusText = "Ready";
        ProgressText = GetIdleProgressText();
        FooterActionText = string.Empty;
    }

    private bool HasValidSourceSelection()
    {
        return HasSelectedFiles &&
            (!ArchiveMode || EncryptMode || SourcePaths.Count == 1) &&
            (ArchiveMode || SourcePaths.Count <= CoreFileCrypter.MaximumBatchFileCount);
    }

    private bool HasValidOutputDirectory()
    {
        return !string.IsNullOrWhiteSpace(OutputDirectory) && Directory.Exists(OutputDirectory);
    }

    private bool HasValidArchiveName()
    {
        return !ArchiveMode ||
            !EncryptMode ||
            ArchiveFileNameHelper.TryCreateArchiveFileName(ArchiveName, out _, out _);
    }

    private string GetBlockingValidationMessage()
    {
        if (!HasSelectedFiles)
        {
            return (ArchiveMode, EncryptMode) switch
            {
                (false, true) => "No batch files selected. Add files to encrypt.",
                (false, false) => "No batch files selected. Add files to decrypt.",
                (true, true) => "No archive files selected. Add files to bundle into one encrypted archive.",
                _ => "No archive selected. Choose one encrypted archive to extract.",
            };
        }

        if (ArchiveMode && !EncryptMode && SourcePaths.Count != 1)
        {
            return "Choose exactly one encrypted archive file to extract.";
        }

        if (!ArchiveMode && SourcePaths.Count > CoreFileCrypter.MaximumBatchFileCount)
        {
            return $"Choose {CoreFileCrypter.MaximumBatchFileCount} files or fewer for one batch run.";
        }

        List<string> issues = [];
        if (string.IsNullOrWhiteSpace(OutputDirectory))
        {
            issues.Add(ArchiveMode && EncryptMode
                ? "Choose an archive output folder."
                : ArchiveMode
                    ? "Choose an extraction folder."
                    : "Choose a batch output folder.");
        }
        else if (!Directory.Exists(OutputDirectory))
        {
            issues.Add("Choose an existing output folder.");
        }

        if (string.IsNullOrWhiteSpace(Password))
        {
            issues.Add(EncryptMode
                ? "Enter a password."
                : "Enter the password used during encryption.");
        }

        if (ArchiveMode &&
            EncryptMode &&
            !ArchiveFileNameHelper.TryCreateArchiveFileName(ArchiveName, out _, out string? archiveNameError))
        {
            issues.Add(archiveNameError ?? "Archive name is invalid.");
        }

        return string.Join(" ", issues);
    }

    private string GetIdleProgressText()
    {
        if (!HasSelectedFiles)
        {
            return ArchiveMode && !EncryptMode
                ? "No archive selected"
                : "No batch files selected";
        }

        FileSelectionPreviewViewModel? firstPreview = SourceFilePreviews.FirstOrDefault();
        if (firstPreview is null)
        {
            return FilesSummaryText;
        }

        if (ArchiveMode && !EncryptMode && SourcePaths.Count == 1)
        {
            return $"{firstPreview.FileName} - {firstPreview.SizeText}";
        }

        if (SourcePaths.Count == 1)
        {
            return $"1 file selected - {firstPreview.FileName}";
        }

        return $"{SourcePaths.Count} files selected - {firstPreview.FileName} + {SourcePaths.Count - 1} more";
    }

    private string GetRunningStatusText()
    {
        return (ArchiveMode, EncryptMode) switch
        {
            (false, true) => "Batch encrypting",
            (false, false) => "Batch decrypting",
            (true, true) => "Archive encrypting",
            _ => "Archive decrypting",
        };
    }

    private string GetStartingProgressText()
    {
        return (ArchiveMode, EncryptMode) switch
        {
            (false, true) => "Starting batch encryption...",
            (false, false) => "Starting batch decryption...",
            (true, true) => "Starting archive encryption...",
            _ => "Starting archive extraction...",
        };
    }

    private string GetFailureProgressText()
    {
        return (ArchiveMode, EncryptMode) switch
        {
            (false, true) => "Batch encryption failed.",
            (false, false) => "Batch decryption failed.",
            (true, true) => "Archive encryption failed.",
            _ => "Archive extraction failed.",
        };
    }

    private string GetFailureStatusText()
    {
        return (ArchiveMode, EncryptMode) switch
        {
            (false, true) => "Batch encryption failed",
            (false, false) => "Batch decryption failed",
            (true, true) => "Archive encryption failed",
            _ => "Archive extraction failed",
        };
    }

    private void RefreshIdleValidationState()
    {
        if (!IsRunning && !HasResults)
        {
            ResetIdleStateForMode();
        }
    }

    private void TrySetDefaultOutputDirectory(string sourcePath)
    {
        if (!string.IsNullOrWhiteSpace(OutputDirectory) || SourcePaths.Count != 0)
        {
            return;
        }

        string? sourceDirectory = Path.GetDirectoryName(sourcePath);
        if (!string.IsNullOrWhiteSpace(sourceDirectory) && Directory.Exists(sourceDirectory))
        {
            SetOutputDirectory(sourceDirectory, OutputDirectoryOrigin.SourceAutofill);
        }
    }

    private void SetOutputDirectory(string value, OutputDirectoryOrigin origin)
    {
        if (string.Equals(OutputDirectory, value, StringComparison.Ordinal) && outputDirectoryOrigin == origin)
        {
            return;
        }

        isUpdatingOutputDirectoryInternally = true;
        try
        {
            outputDirectoryOrigin = origin;
            OutputDirectory = value;
        }
        finally
        {
            isUpdatingOutputDirectoryInternally = false;
        }
    }

    private static string GetUsableDefaultOutputDirectory(string? path)
    {
        return !string.IsNullOrWhiteSpace(path) && Directory.Exists(path)
            ? path
            : string.Empty;
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

    private string BuildResultsClipboardText()
    {
        StringBuilder builder = new();
        if (HasResultSummary)
        {
            builder.AppendLine(ResultSummary);
            builder.AppendLine();
        }

        foreach (BatchResultItemViewModel item in Results)
        {
            builder.AppendLine($"{item.StatusLabel}: {item.TitleText}");

            if (!string.IsNullOrWhiteSpace(item.PrimaryText))
            {
                builder.AppendLine(item.PrimaryText);
            }

            if (!string.IsNullOrWhiteSpace(item.DetailText))
            {
                builder.AppendLine($"{item.DetailLabel}: {item.DetailText}");
            }

            if (item.HasSecondaryText)
            {
                builder.AppendLine(item.SecondaryText);
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private void RaiseToast(WorkflowToastKind kind, string title, string message, string detail = "")
    {
        ToastNotificationRequested?.Invoke(this, new WorkflowToastNotification(kind, title, message, detail));
    }

    private enum OutputDirectoryOrigin
    {
        None,
        SettingsDefault,
        SourceAutofill,
        Manual,
    }
}

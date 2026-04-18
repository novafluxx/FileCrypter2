using System.Collections.ObjectModel;
using System.Collections.Specialized;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;
using FileCrypter.Core;
using CoreFileCrypter = FileCrypter.Core.FileCrypter;

namespace FileCrypter.App.ViewModels;

public sealed partial class BatchViewModel : ViewModelBase, IWorkflowStatusViewModel
{
    private readonly IFileCrypterWorkflowService workflowService;
    private readonly IFilePickerService? filePickerService;
    private readonly StringComparer pathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

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
    private string errorMessage = "No batch files selected. Add files to encrypt.";

    [ObservableProperty]
    private string resultSummary = string.Empty;

    public BatchViewModel(
        IFileCrypterWorkflowService workflowService,
        IFilePickerService? filePickerService = null)
    {
        this.workflowService = workflowService;
        this.filePickerService = filePickerService;

        SourcePaths.CollectionChanged += OnSourcePathsChanged;
        Results.CollectionChanged += OnResultsChanged;
    }

    public string Title => "Batch workflows";

    public ObservableCollection<string> SourcePaths { get; } = [];

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

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasResults => Results.Count > 0;

    public bool HasResultSummary => !string.IsNullOrWhiteSpace(ResultSummary);

    public bool HasFailures => Results.Any(item => !item.Succeeded);

    public bool HasKeyFileChoice => !string.IsNullOrWhiteSpace(KeyFilePath);

    public bool ShowArchiveNameEditor => ArchiveMode && EncryptMode;

    public string ModeTitle => (ArchiveMode, EncryptMode) switch
    {
        (false, true) => "Encrypt many files at once",
        (false, false) => "Decrypt many files at once",
        (true, true) => "Create one encrypted archive",
        _ => "Decrypt and extract one encrypted archive",
    };

    public string ModeDescription => (ArchiveMode, EncryptMode) switch
    {
        (false, true) =>
            "Each selected file becomes its own encrypted output, and batch encryption turns compression on automatically.",
        (false, false) =>
            "Each selected file is decrypted separately, removing the .encrypted suffix when it can and using a safe fallback when it cannot.",
        (true, true) =>
            "Bundle multiple files into one compressed .tar.zst.encrypted archive with an optional custom archive name.",
        _ =>
            "Decrypt one encrypted archive and extract every file into the chosen output folder while preserving overwrite protection.",
    };

    public string WorkflowKindDescription => ArchiveMode
        ? "Archive mode creates one encrypted archive from many files or extracts one encrypted archive back into a folder."
        : "Individual-file mode processes every selected file separately inside one batch run.";

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

    public string BrowseFilesButtonText => ArchiveMode && !EncryptMode ? "Choose archive" : "Add files";

    public string OutputFolderPickerTitle => ArchiveMode && EncryptMode
        ? "Choose archive output directory"
        : ArchiveMode
            ? "Choose archive extraction directory"
            : "Choose batch output directory";

    partial void OnEncryptModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IsDecryptMode));
        OnWorkflowModeChanged();
    }

    partial void OnArchiveModeChanged(bool value)
    {
        OnPropertyChanged(nameof(IndividualFilesMode));
        OnWorkflowModeChanged();
    }

    partial void OnKeyFilePathChanged(string value)
    {
        OnPropertyChanged(nameof(HasKeyFileChoice));
        OnPropertyChanged(nameof(KeyFileChoiceStatusText));
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnResultSummaryChanged(string value)
    {
        OnPropertyChanged(nameof(HasResultSummary));
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
            OutputDirectory = Path.GetFullPath(selectedPath);
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
        IsRunning = true;
        ErrorMessage = string.Empty;
        ProgressPercent = 0;
        StatusText = GetRunningStatusText();
        ProgressText = GetStartingProgressText();
        ClearResults();

        try
        {
            if (ArchiveMode)
            {
                if (EncryptMode)
                {
                    await RunArchiveEncryptAsync();
                }
                else
                {
                    await RunArchiveDecryptAsync();
                }
            }
            else
            {
                await RunIndividualBatchAsync();
            }
        }
        catch (Exception exception)
        {
            ErrorMessage = WorkflowErrorMessageFormatter.GetTroubleshootingMessage(exception);
            ProgressText = GetFailureProgressText();
            StatusText = "Ready";
        }
        finally
        {
            IsRunning = false;
        }
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
            !string.IsNullOrWhiteSpace(OutputDirectory) &&
            !string.IsNullOrWhiteSpace(Password);
    }

    private void OnSourcePathsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (!string.IsNullOrWhiteSpace(SelectedSourcePath) &&
            !SourcePaths.Any(path => pathComparer.Equals(path, SelectedSourcePath)))
        {
            SelectedSourcePath = null;
        }

        OnPropertyChanged(nameof(HasSelectedFiles));
        OnPropertyChanged(nameof(FilesSummaryText));
        StartBatchCommand.NotifyCanExecuteChanged();
        RemoveSelectedFileCommand.NotifyCanExecuteChanged();
        ClearFilesCommand.NotifyCanExecuteChanged();

        if (HasSelectedFiles && string.IsNullOrWhiteSpace(ResultSummary))
        {
            ErrorMessage = GetSelectionValidationMessage();
            ProgressText = FilesSummaryText;
        }
        else if (!HasSelectedFiles && !HasResults && !IsRunning)
        {
            ResetIdleStateForMode();
        }
    }

    private void OnResultsChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        OnPropertyChanged(nameof(HasResults));
        OnPropertyChanged(nameof(HasFailures));
    }

    private void OnWorkflowModeChanged()
    {
        OnPropertyChanged(nameof(ModeTitle));
        OnPropertyChanged(nameof(ModeDescription));
        OnPropertyChanged(nameof(WorkflowKindDescription));
        OnPropertyChanged(nameof(SourceHeading));
        OnPropertyChanged(nameof(OutputHeading));
        OnPropertyChanged(nameof(OutputDescription));
        OnPropertyChanged(nameof(ShowArchiveNameEditor));
        OnPropertyChanged(nameof(ArchiveNameStatusText));
        OnPropertyChanged(nameof(FilesSummaryText));
        OnPropertyChanged(nameof(KeyFileChoiceStatusText));
        OnPropertyChanged(nameof(ActionButtonText));
        OnPropertyChanged(nameof(BrowseFilesButtonText));
        OnPropertyChanged(nameof(OutputFolderPickerTitle));

        StartBatchCommand.NotifyCanExecuteChanged();
        ClearResults();
        ResetIdleStateForMode();
    }

    private async Task RunIndividualBatchAsync()
    {
        BatchTransformRequest request = new(
            SourcePaths.ToArray(),
            OutputDirectory,
            Password,
            NeverOverwriteExistingFiles,
            string.IsNullOrWhiteSpace(KeyFilePath) ? null : KeyFilePath);

        Progress<BatchOperationProgress> progress = new(ReportBatchProgress);
        BatchTransformResult result = EncryptMode
            ? await workflowService.EncryptFilesAsync(request, progress, CancellationToken.None)
            : await workflowService.DecryptFilesAsync(request, progress, CancellationToken.None);

        ApplyBatchResults(result);
        ProgressPercent = 100;
        StatusText = "Ready";
        ProgressText = result.Succeeded
            ? EncryptMode ? "Batch encryption complete." : "Batch decryption complete."
            : $"{result.SucceededCount} succeeded, {result.FailedCount} failed.";
        ResultSummary = result.Succeeded
            ? EncryptMode
                ? $"Batch encryption complete. {result.SucceededCount} file(s) succeeded."
                : $"Batch decryption complete. {result.SucceededCount} file(s) succeeded."
            : EncryptMode
                ? $"Batch encryption finished with {result.FailedCount} failure(s) and {result.SucceededCount} success(es)."
                : $"Batch decryption finished with {result.FailedCount} failure(s) and {result.SucceededCount} success(es).";
    }

    private async Task RunArchiveEncryptAsync()
    {
        ArchiveEncryptRequest request = new(
            SourcePaths.ToArray(),
            OutputDirectory,
            Password,
            NeverOverwriteExistingFiles,
            string.IsNullOrWhiteSpace(KeyFilePath) ? null : KeyFilePath,
            string.IsNullOrWhiteSpace(ArchiveName) ? null : ArchiveName);

        Progress<FileCrypterProgress> progress = new(ReportArchiveProgress);
        ArchiveEncryptResult result = await workflowService.EncryptArchiveAsync(
            request,
            progress,
            CancellationToken.None);

        Results.Add(new BatchResultItemViewModel(
            succeeded: true,
            statusLabel: "Created",
            statusBrush: "#b8e6c2",
            titleText: Path.GetFileName(result.OutputPath),
            primaryText: result.OutputPath,
            detailLabel: "CONTENTS",
            detailText: $"{SourcePaths.Count} selected file(s) bundled into this encrypted archive.",
            detailBrush: "#d8f1de",
            secondaryText: string.Empty));
        ProgressPercent = 100;
        StatusText = "Ready";
        ProgressText = "Archive encryption complete.";
        ResultSummary = $"Archive encryption complete. {SourcePaths.Count} file(s) bundled into one encrypted archive.";
    }

    private async Task RunArchiveDecryptAsync()
    {
        ArchiveDecryptRequest request = new(
            SourcePaths.Single(),
            OutputDirectory,
            Password,
            NeverOverwriteExistingFiles,
            string.IsNullOrWhiteSpace(KeyFilePath) ? null : KeyFilePath);

        Progress<FileCrypterProgress> progress = new(ReportArchiveProgress);
        ArchiveDecryptResult result = await workflowService.DecryptArchiveAsync(
            request,
            progress,
            CancellationToken.None);

        foreach (string outputPath in result.OutputPaths)
        {
            Results.Add(new BatchResultItemViewModel(
                succeeded: true,
                statusLabel: "Extracted",
                statusBrush: "#b8e6c2",
                titleText: Path.GetFileName(outputPath),
                primaryText: outputPath,
                detailLabel: "SOURCE ARCHIVE",
                detailText: request.SourcePath,
                detailBrush: "#d8f1de",
                secondaryText: string.Empty));
        }

        ProgressPercent = 100;
        StatusText = "Ready";
        ProgressText = "Archive extraction complete.";
        ResultSummary = $"Archive extraction complete. {result.OutputPaths.Count} file(s) extracted.";
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

            SourcePaths.Add(fullPath);
        }
    }

    private void SetSourcePaths(IEnumerable<string> selectedPaths)
    {
        SourcePaths.Clear();
        AddSourcePaths(selectedPaths);
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
                    statusBrush: "#b8e6c2",
                    titleText: Path.GetFileName(item.InputPath),
                    primaryText: item.InputPath,
                    detailLabel: "OUTPUT",
                    detailText: outputPath,
                    detailBrush: "#d8f1de",
                    secondaryText: string.Empty));
            }
            else
            {
                Results.Add(new BatchResultItemViewModel(
                    succeeded: false,
                    statusLabel: "Failed",
                    statusBrush: "#f0c77d",
                    titleText: Path.GetFileName(item.InputPath),
                    primaryText: item.InputPath,
                    detailLabel: "ISSUE",
                    detailText: WorkflowErrorMessageFormatter.GetTroubleshootingMessage(item.Error!),
                    detailBrush: "#ffe0ab",
                    secondaryText: $"Requested output: {item.RequestedOutputPath}"));
            }
        }
    }

    private void ReportBatchProgress(BatchOperationProgress progress)
    {
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
            ? $"{(EncryptMode ? "Encrypting" : "Decrypting")} file {currentFileNumber} of {progress.TotalFiles}: {currentFileName} ({progress.CurrentInputBytes}/{progress.CurrentTotalInputBytes.Value} bytes)"
            : $"{(EncryptMode ? "Encrypting" : "Decrypting")} file {currentFileNumber} of {progress.TotalFiles}: {currentFileName}";
    }

    private void ReportArchiveProgress(FileCrypterProgress progress)
    {
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
            ? $"{phaseText}: {progress.InputBytes}/{progress.TotalInputBytes.Value} bytes"
            : phaseText;
    }

    private void ResetIdleStateForMode()
    {
        if (IsRunning || HasResults)
        {
            return;
        }

        ErrorMessage = GetSelectionValidationMessage();
        ProgressText = GetIdleProgressText();
    }

    private bool HasValidSourceSelection()
    {
        return HasSelectedFiles && (!ArchiveMode || EncryptMode || SourcePaths.Count == 1);
    }

    private string GetSelectionValidationMessage()
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

        return ArchiveMode && !EncryptMode && SourcePaths.Count != 1
            ? "Choose exactly one encrypted archive file to extract."
            : string.Empty;
    }

    private string GetIdleProgressText()
    {
        if (HasSelectedFiles)
        {
            return FilesSummaryText;
        }

        return ArchiveMode && !EncryptMode
            ? "No archive selected"
            : "No batch files selected";
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
}

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;
using FileCrypter.Core;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.ViewModels;

public sealed partial class DecryptViewModel : ViewModelBase, IWorkflowStatusViewModel
{
    private const string DefaultEncryptedSuffix = ".encrypted";
    private const string DefaultDecryptedSuffix = ".decrypted";

    private readonly IFileCrypterWorkflowService workflowService;
    private readonly IFilePickerService? filePickerService;
    private string defaultOutputDirectory = string.Empty;
    private OutputPathOrigin outputPathOrigin;
    private bool isUpdatingOutputPathInternally;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDecryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseSourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseOutputCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearKeyFileCommand))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDecryptCommand))]
    private string sourcePath = string.Empty;

    [ObservableProperty]
    private string outputPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartDecryptCommand))]
    private string password = string.Empty;

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
    private string errorMessage = "No encrypted file selected. Choose a file to decrypt.";

    [ObservableProperty]
    private string resultPath = string.Empty;

    public DecryptViewModel(
        IFileCrypterWorkflowService workflowService,
        IFilePickerService? filePickerService = null)
    {
        this.workflowService = workflowService;
        this.filePickerService = filePickerService;
    }

    public DecryptViewModel(
        IFileCrypterWorkflowService workflowService,
        FileCrypterSettings initialSettings,
        IFilePickerService? filePickerService = null)
    {
        this.workflowService = workflowService;
        this.filePickerService = filePickerService;
        ApplySettings(initialSettings);
    }

    public string Title => "Decrypt a file";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasResult => !string.IsNullOrWhiteSpace(ResultPath);

    public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SourcePath);

    public bool HasKeyFileChoice => !string.IsNullOrWhiteSpace(KeyFilePath);

    public string KeyFileChoiceStatusText => HasKeyFileChoice
        ? "Using the selected key file as the optional second factor."
        : "No key file selected. Decryption will use only the password unless the file requires one.";

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
    }

    partial void OnSourcePathChanged(string value)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(value)
            ? "No encrypted file selected. Choose a file to decrypt."
            : string.Empty;
        ProgressText = string.IsNullOrWhiteSpace(value) ? "No encrypted file selected" : Path.GetFileName(value);
        OnPropertyChanged(nameof(HasSelectedFile));
        RefreshSuggestedOutputPath();
    }

    partial void OnOutputPathChanged(string value)
    {
        if (!isUpdatingOutputPathInternally)
        {
            outputPathOrigin = string.IsNullOrWhiteSpace(value)
                ? OutputPathOrigin.None
                : OutputPathOrigin.Manual;
        }

        OnPropertyChanged(nameof(OutputDisplayText));
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

    partial void OnResultPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasResult));
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseSourceAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        string? selectedPath = await filePickerService.PickOpenFileAsync(
            "Choose an encrypted file to decrypt",
            CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            SourcePath = selectedPath;
        }
    }

    [RelayCommand(CanExecute = nameof(CanBrowse))]
    private async Task BrowseOutputAsync()
    {
        if (filePickerService is null)
        {
            return;
        }

        string? selectedPath = await filePickerService.PickSaveFileAsync(
            "Choose decrypted output file",
            GetSuggestedOutputFileName(),
            CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            OutputPath = selectedPath;
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
            "Choose the matching key file",
            CancellationToken.None);
        if (!string.IsNullOrWhiteSpace(selectedPath))
        {
            KeyFilePath = selectedPath;
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
        IsRunning = true;
        ErrorMessage = string.Empty;
        ResultPath = string.Empty;
        ProgressPercent = 0;
        ProgressText = "Starting decryption...";
        StatusText = "Decrypting";

        try
        {
            DecryptFileRequest request = new(
                SourcePath,
                string.IsNullOrWhiteSpace(OutputPath) ? null : OutputPath,
                Password,
                NeverOverwriteExistingFiles,
                string.IsNullOrWhiteSpace(KeyFilePath) ? null : KeyFilePath);

            Progress<FileCrypterProgress> progress = new(ReportProgress);
            DecryptFileResult result = await workflowService.DecryptFileAsync(
                request,
                progress,
                CancellationToken.None);

            ResultPath = result.OutputPath;
            ProgressPercent = 100;
            ProgressText = "Decryption complete.";
            StatusText = "Ready";
        }
        catch (Exception exception)
        {
            ErrorMessage = WorkflowErrorMessageFormatter.GetTroubleshootingMessage(exception);
            ProgressText = "Decryption failed.";
            StatusText = "Ready";
        }
        finally
        {
            IsRunning = false;
        }
    }

    private bool CanBrowse()
    {
        return !IsRunning;
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
        return !IsRunning &&
            !string.IsNullOrWhiteSpace(SourcePath) &&
            !string.IsNullOrWhiteSpace(Password);
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

    private void ReportProgress(FileCrypterProgress progress)
    {
        if (progress.TotalInputBytes is > 0)
        {
            ProgressPercent = Math.Clamp(
                progress.InputBytes * 100d / progress.TotalInputBytes.Value,
                0,
                100);
            ProgressText = $"{progress.InputBytes}/{progress.TotalInputBytes.Value} bytes";
        }
        else
        {
            ProgressText = $"{progress.InputBytes} bytes processed";
        }
    }

    private enum OutputPathOrigin
    {
        None,
        SettingsDefault,
        Manual,
    }
}

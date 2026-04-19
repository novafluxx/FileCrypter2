using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;
using FileCrypter.Core;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.ViewModels;

public sealed partial class EncryptViewModel : ViewModelBase, IWorkflowStatusViewModel
{
    private readonly IFileCrypterWorkflowService workflowService;
    private readonly IFilePickerService? filePickerService;
    private string defaultOutputDirectory = string.Empty;
    private OutputPathOrigin outputPathOrigin;
    private bool isUpdatingOutputPathInternally;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartEncryptCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseSourceCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseOutputCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseGeneratedKeyFileCommand))]
    private bool isRunning;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartEncryptCommand))]
    private string sourcePath = string.Empty;

    [ObservableProperty]
    private string outputPath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartEncryptCommand))]
    private string password = string.Empty;

    [ObservableProperty]
    private bool enableCompression;

    [ObservableProperty]
    private bool neverOverwriteExistingFiles = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseGeneratedKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearKeyFileCommand))]
    private string keyFilePath = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(BrowseKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(BrowseGeneratedKeyFileCommand))]
    [NotifyCanExecuteChangedFor(nameof(ClearGeneratedKeyFileCommand))]
    private string generateKeyFilePath = string.Empty;

    [ObservableProperty]
    private double progressPercent;

    [ObservableProperty]
    private string statusText = "Ready";

    [ObservableProperty]
    private string progressText = "No file selected";

    [ObservableProperty]
    private string errorMessage = "No files selected. Choose a file to encrypt.";

    [ObservableProperty]
    private string resultPath = string.Empty;

    [ObservableProperty]
    private string generatedKeyFileResultPath = string.Empty;

    public EncryptViewModel(
        IFileCrypterWorkflowService workflowService,
        IFilePickerService? filePickerService = null)
        : this(workflowService, enableCompressionByDefault: false, filePickerService)
    {
    }

    public EncryptViewModel(
        IFileCrypterWorkflowService workflowService,
        bool enableCompressionByDefault,
        IFilePickerService? filePickerService = null)
    {
        this.workflowService = workflowService;
        this.filePickerService = filePickerService;
        EnableCompression = enableCompressionByDefault;
    }

    public EncryptViewModel(
        IFileCrypterWorkflowService workflowService,
        FileCrypterSettings initialSettings,
        IFilePickerService? filePickerService = null)
    {
        this.workflowService = workflowService;
        this.filePickerService = filePickerService;
        ApplySettings(initialSettings);
    }

    public string Title => "Encrypt a file";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasResult => !string.IsNullOrWhiteSpace(ResultPath);

    public bool HasGeneratedKeyFileResult => !string.IsNullOrWhiteSpace(GeneratedKeyFileResultPath);

    public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SourcePath);

    public bool HasExistingKeyFileChoice => !string.IsNullOrWhiteSpace(KeyFilePath);

    public bool HasGeneratedKeyFileChoice => !string.IsNullOrWhiteSpace(GenerateKeyFilePath);

    public bool HasAnyKeyFileChoice => HasExistingKeyFileChoice || HasGeneratedKeyFileChoice;

    public bool CanEditExistingKeyFileChoice => !IsRunning && !HasGeneratedKeyFileChoice;

    public bool CanEditGeneratedKeyFileChoice => !IsRunning && !HasExistingKeyFileChoice;

    public string KeyFileChoiceStatusText => HasExistingKeyFileChoice
        ? "Using an existing key file as the optional second factor."
        : HasGeneratedKeyFileChoice
            ? "A new key file will be generated for this encryption run."
            : "No key file selected. Encryption will use only the password.";

    public string OutputDisplayText => string.IsNullOrWhiteSpace(OutputPath)
        ? "Auto-generated from input filename..."
        : OutputPath;

    public void ApplySettings(FileCrypterSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);

        EnableCompression = settings.EnableCompressionByDefault;
        NeverOverwriteExistingFiles = settings.NeverOverwriteExistingFilesByDefault;
        defaultOutputDirectory = GetUsableDefaultOutputDirectory(settings.DefaultOutputDirectory);
        RefreshSuggestedOutputPath();
    }

    partial void OnSourcePathChanged(string value)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(value)
            ? "No files selected. Choose a file to encrypt."
            : string.Empty;
        ProgressText = string.IsNullOrWhiteSpace(value) ? "No file selected" : Path.GetFileName(value);
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

    partial void OnIsRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(CanEditExistingKeyFileChoice));
        OnPropertyChanged(nameof(CanEditGeneratedKeyFileChoice));
    }

    partial void OnKeyFilePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(GenerateKeyFilePath))
        {
            GenerateKeyFilePath = string.Empty;
        }

        OnKeyFileChoiceStateChanged();
    }

    partial void OnGenerateKeyFilePathChanged(string value)
    {
        if (!string.IsNullOrWhiteSpace(value) && !string.IsNullOrWhiteSpace(KeyFilePath))
        {
            KeyFilePath = string.Empty;
        }

        OnKeyFileChoiceStateChanged();
    }

    partial void OnErrorMessageChanged(string value)
    {
        OnPropertyChanged(nameof(HasError));
    }

    partial void OnResultPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasResult));
    }

    partial void OnGeneratedKeyFileResultPathChanged(string value)
    {
        OnPropertyChanged(nameof(HasGeneratedKeyFileResult));
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
        IsRunning = true;
        ErrorMessage = string.Empty;
        ResultPath = string.Empty;
        GeneratedKeyFileResultPath = string.Empty;
        ProgressPercent = 0;
        ProgressText = "Starting encryption...";
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

            Progress<FileCrypterProgress> progress = new(ReportProgress);
            EncryptFileResult result = await workflowService.EncryptFileAsync(
                request,
                progress,
                CancellationToken.None);

            ResultPath = result.OutputPath;
            GeneratedKeyFileResultPath = result.GeneratedKeyFilePath ?? string.Empty;
            ProgressPercent = 100;
            ProgressText = "Encryption complete.";
            StatusText = "Ready";
        }
        catch (Exception exception)
        {
            ErrorMessage = GetTroubleshootingMessage(exception);
            ProgressText = "Encryption failed.";
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
        return !IsRunning &&
            !string.IsNullOrWhiteSpace(SourcePath) &&
            !string.IsNullOrWhiteSpace(Password);
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

    private void OnKeyFileChoiceStateChanged()
    {
        OnPropertyChanged(nameof(HasExistingKeyFileChoice));
        OnPropertyChanged(nameof(HasGeneratedKeyFileChoice));
        OnPropertyChanged(nameof(HasAnyKeyFileChoice));
        OnPropertyChanged(nameof(CanEditExistingKeyFileChoice));
        OnPropertyChanged(nameof(CanEditGeneratedKeyFileChoice));
        OnPropertyChanged(nameof(KeyFileChoiceStatusText));
    }

    private static string GetTroubleshootingMessage(Exception exception)
    {
        return WorkflowErrorMessageFormatter.GetTroubleshootingMessage(exception);
    }

    private enum OutputPathOrigin
    {
        None,
        SettingsDefault,
        Manual,
    }
}

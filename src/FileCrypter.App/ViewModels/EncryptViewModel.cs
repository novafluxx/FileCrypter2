using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using FileCrypter.App.Services;
using FileCrypter.Core;
using FileCrypter.Core.Format;

namespace FileCrypter.App.ViewModels;

public sealed partial class EncryptViewModel : ViewModelBase
{
    private readonly IFileCrypterWorkflowService workflowService;
    private readonly IFilePickerService? filePickerService;

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
    private string keyFilePath = string.Empty;

    [ObservableProperty]
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
    {
        this.workflowService = workflowService;
        this.filePickerService = filePickerService;
    }

    public string Title => "Encrypt a file";

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorMessage);

    public bool HasResult => !string.IsNullOrWhiteSpace(ResultPath);

    public bool HasGeneratedKeyFileResult => !string.IsNullOrWhiteSpace(GeneratedKeyFileResultPath);

    public bool HasSelectedFile => !string.IsNullOrWhiteSpace(SourcePath);

    public string OutputDisplayText => string.IsNullOrWhiteSpace(OutputPath)
        ? "Auto-generated from input filename..."
        : OutputPath;

    partial void OnSourcePathChanged(string value)
    {
        ErrorMessage = string.IsNullOrWhiteSpace(value)
            ? "No files selected. Choose a file to encrypt."
            : string.Empty;
        ProgressText = string.IsNullOrWhiteSpace(value) ? "No file selected" : Path.GetFileName(value);
        OnPropertyChanged(nameof(HasSelectedFile));
    }

    partial void OnOutputPathChanged(string value)
    {
        OnPropertyChanged(nameof(OutputDisplayText));
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

    [RelayCommand(CanExecute = nameof(CanBrowse))]
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

    [RelayCommand(CanExecute = nameof(CanBrowse))]
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

    private static string GetTroubleshootingMessage(Exception exception)
    {
        if (exception is FileCrypterFormatException formatException)
        {
            return formatException.Code switch
            {
                FileCrypterFormatErrorCode.AuthenticationFailed =>
                    "Check the password and key file, then try again.",
                FileCrypterFormatErrorCode.KeyFileRequired =>
                    "Provide the matching key file.",
                FileCrypterFormatErrorCode.InvalidMagic =>
                    "Choose a FileCrypter .encrypted file produced by this app.",
                FileCrypterFormatErrorCode.TruncatedHeader or FileCrypterFormatErrorCode.TruncatedChunk =>
                    "The encrypted file appears incomplete or damaged. Try a fresh copy of the file.",
                _ => "The encrypted file metadata or payload is not valid for this FileCrypter version.",
            };
        }

        if (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return $"Path error: {exception.Message}";
        }

        return exception.Message;
    }
}

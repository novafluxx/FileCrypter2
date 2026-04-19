using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;
using FileCrypter.Core;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.Tests.ViewModels;

public sealed class EncryptViewModelTests
{
    [Fact]
    public void StartEncryptCommand_RequiresSourcePathAndPassword()
    {
        var viewModel = new EncryptViewModel(new RecordingWorkflowService());

        Assert.False(viewModel.StartEncryptCommand.CanExecute(null));

        viewModel.SourcePath = "/tmp/plain.txt";
        Assert.False(viewModel.StartEncryptCommand.CanExecute(null));

        viewModel.Password = "secret";
        Assert.True(viewModel.StartEncryptCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartEncryptCommand_WhenSuccessful_ReportsResultAndReEnablesCommand()
    {
        var workflow = new RecordingWorkflowService
        {
            Result = new EncryptFileResult("/tmp/plain.txt.encrypted", null),
        };
        var viewModel = CreateReadyViewModel(workflow);

        await viewModel.StartEncryptCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/plain.txt.encrypted", viewModel.ResultPath);
        Assert.True(viewModel.HasResult);
        Assert.Empty(viewModel.ErrorMessage);
        Assert.True(viewModel.StartEncryptCommand.CanExecute(null));
        Assert.Equal("Ready", viewModel.StatusText);
    }

    [Fact]
    public async Task StartEncryptCommand_PassesCompressionToggleToWorkflow()
    {
        var workflow = new RecordingWorkflowService();
        var viewModel = CreateReadyViewModel(workflow);
        viewModel.EnableCompression = true;

        await viewModel.StartEncryptCommand.ExecuteAsync(null);

        Assert.NotNull(workflow.Request);
        Assert.True(workflow.Request.EnableCompression);
    }

    [Fact]
    public async Task StartEncryptCommand_WhileRunning_DisablesCommand()
    {
        var workflow = new WaitingWorkflowService();
        var viewModel = CreateReadyViewModel(workflow);

        Task runTask = viewModel.StartEncryptCommand.ExecuteAsync(null);
        await workflow.Started.Task;

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.StartEncryptCommand.CanExecute(null));

        workflow.Finish(new EncryptFileResult("/tmp/out.encrypted", null));
        await runTask;

        Assert.False(viewModel.IsRunning);
        Assert.True(viewModel.StartEncryptCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartEncryptCommand_WhenWorkflowFails_ShowsErrorWithoutResult()
    {
        var workflow = new RecordingWorkflowService
        {
            Error = new IOException("The output directory is missing."),
        };
        var viewModel = CreateReadyViewModel(workflow);

        await viewModel.StartEncryptCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.ResultPath);
        Assert.True(viewModel.HasError);
        Assert.Contains("Path error:", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Contains("output directory is missing", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal("Ready", viewModel.StatusText);
    }

    [Fact]
    public void SettingExistingKeyFile_ClearsGeneratedKeyFileChoiceAndUpdatesStatus()
    {
        var viewModel = CreateReadyViewModel(new RecordingWorkflowService());
        viewModel.GenerateKeyFilePath = "/tmp/generated.key";

        viewModel.KeyFilePath = "/tmp/existing.key";

        Assert.Equal("/tmp/existing.key", viewModel.KeyFilePath);
        Assert.Empty(viewModel.GenerateKeyFilePath);
        Assert.True(viewModel.HasExistingKeyFileChoice);
        Assert.False(viewModel.HasGeneratedKeyFileChoice);
        Assert.False(viewModel.CanEditGeneratedKeyFileChoice);
        Assert.Contains("existing key file", viewModel.KeyFileChoiceStatusText, StringComparison.Ordinal);
    }

    [Fact]
    public async Task BrowseGeneratedKeyFileCommand_ClearsExistingKeyFileChoice()
    {
        var picker = new RecordingFilePickerService
        {
            SaveResult = "/tmp/generated.key",
        };
        var viewModel = new EncryptViewModel(new RecordingWorkflowService(), picker)
        {
            SourcePath = "/tmp/plain.txt",
            Password = "secret",
            KeyFilePath = "/tmp/existing.key",
        };

        viewModel.ClearKeyFileCommand.Execute(null);
        await viewModel.BrowseGeneratedKeyFileCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/generated.key", viewModel.GenerateKeyFilePath);
        Assert.Empty(viewModel.KeyFilePath);
        Assert.Equal("Choose where to save a new key file", picker.LastSaveTitle);
        Assert.Equal("filecrypter.key", picker.LastSuggestedFileName);
    }

    [Fact]
    public async Task BrowseKeyFileCommand_ClearsGeneratedKeyFileChoice()
    {
        var picker = new RecordingFilePickerService
        {
            OpenResult = "/tmp/existing.key",
        };
        var viewModel = new EncryptViewModel(new RecordingWorkflowService(), picker)
        {
            SourcePath = "/tmp/plain.txt",
            Password = "secret",
            GenerateKeyFilePath = "/tmp/generated.key",
        };

        viewModel.ClearGeneratedKeyFileCommand.Execute(null);
        await viewModel.BrowseKeyFileCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/existing.key", viewModel.KeyFilePath);
        Assert.Empty(viewModel.GenerateKeyFilePath);
        Assert.Equal("Choose an existing key file", picker.LastOpenTitle);
    }

    [Fact]
    public void Constructor_WithSharedSettings_AppliesOverwriteAndDefaultOutputDirectory()
    {
        using var outputDirectory = new TemporaryDirectory();
        var viewModel = new EncryptViewModel(
            new RecordingWorkflowService(),
            new FileCrypterSettings
            {
                EnableCompressionByDefault = true,
                NeverOverwriteExistingFilesByDefault = false,
                DefaultOutputDirectory = outputDirectory.Path,
            });

        viewModel.SourcePath = "/tmp/plain.txt";

        Assert.True(viewModel.EnableCompression);
        Assert.False(viewModel.NeverOverwriteExistingFiles);
        Assert.Equal(Path.Combine(outputDirectory.Path, "plain.txt.encrypted"), viewModel.OutputPath);
    }

    private static EncryptViewModel CreateReadyViewModel(IFileCrypterWorkflowService workflow)
    {
        return new EncryptViewModel(workflow)
        {
            SourcePath = "/tmp/plain.txt",
            Password = "secret",
            NeverOverwriteExistingFiles = true,
        };
    }

    private sealed class RecordingWorkflowService : IFileCrypterWorkflowService
    {
        public EncryptFileRequest? Request { get; private set; }

        public EncryptFileResult Result { get; init; } = new("/tmp/out.encrypted", null);

        public Exception? Error { get; init; }

        public Task<EncryptFileResult> EncryptFileAsync(
            EncryptFileRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            Request = request;
            progress?.Report(new FileCrypterProgress(10, 20) { TotalInputBytes = 10 });

            return Error is null
                ? Task.FromResult(Result)
                : Task.FromException<EncryptFileResult>(Error);
        }

        public Task<DecryptFileResult> DecryptFileAsync(
            DecryptFileRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ArchiveEncryptResult> EncryptArchiveAsync(
            ArchiveEncryptRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ArchiveDecryptResult> DecryptArchiveAsync(
            ArchiveDecryptRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BatchTransformResult> EncryptFilesAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BatchTransformResult> DecryptFilesAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class WaitingWorkflowService : IFileCrypterWorkflowService
    {
        private readonly TaskCompletionSource<EncryptFileResult> completion = new();

        public TaskCompletionSource Started { get; } = new();

        public Task<EncryptFileResult> EncryptFileAsync(
            EncryptFileRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            Started.SetResult();
            return completion.Task;
        }

        public Task<DecryptFileResult> DecryptFileAsync(
            DecryptFileRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ArchiveEncryptResult> EncryptArchiveAsync(
            ArchiveEncryptRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<ArchiveDecryptResult> DecryptArchiveAsync(
            ArchiveDecryptRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BatchTransformResult> EncryptFilesAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<BatchTransformResult> DecryptFilesAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public void Finish(EncryptFileResult result)
        {
            completion.SetResult(result);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }

    private sealed class RecordingFilePickerService : IFilePickerService
    {
        public string? OpenResult { get; init; }

        public string? SaveResult { get; init; }

        public string? LastOpenTitle { get; private set; }

        public string? LastSaveTitle { get; private set; }

        public string? LastSuggestedFileName { get; private set; }

        public Task<string?> PickOpenFileAsync(string title, CancellationToken cancellationToken)
        {
            LastOpenTitle = title;
            return Task.FromResult(OpenResult);
        }

        public Task<IReadOnlyList<string>> PickOpenFilesAsync(string title, CancellationToken cancellationToken)
        {
            LastOpenTitle = title;
            return Task.FromResult<IReadOnlyList<string>>(OpenResult is null ? [] : [OpenResult]);
        }

        public Task<string?> PickOpenFolderAsync(string title, CancellationToken cancellationToken)
        {
            LastOpenTitle = title;
            return Task.FromResult(OpenResult);
        }

        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName, CancellationToken cancellationToken)
        {
            LastSaveTitle = title;
            LastSuggestedFileName = suggestedFileName;
            return Task.FromResult(SaveResult);
        }
    }
}

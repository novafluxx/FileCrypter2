using System.IO;
using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;
using FileCrypter.Core;
using FileCrypter.Core.Format;

namespace FileCrypter.App.Tests.ViewModels;

public sealed class DecryptViewModelTests
{
    [Fact]
    public void StartDecryptCommand_RequiresSourcePathAndPassword()
    {
        var viewModel = new DecryptViewModel(new RecordingWorkflowService());

        Assert.False(viewModel.StartDecryptCommand.CanExecute(null));

        viewModel.SourcePath = "/tmp/plain.txt.encrypted";
        Assert.False(viewModel.StartDecryptCommand.CanExecute(null));

        viewModel.Password = "secret";
        Assert.True(viewModel.StartDecryptCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartDecryptCommand_WhenSuccessful_ReportsResultAndReEnablesCommand()
    {
        var workflow = new RecordingWorkflowService
        {
            Result = new DecryptFileResult("/tmp/plain.txt"),
        };
        var viewModel = CreateReadyViewModel(workflow);

        await viewModel.StartDecryptCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/plain.txt", viewModel.ResultPath);
        Assert.True(viewModel.HasResult);
        Assert.Empty(viewModel.ErrorMessage);
        Assert.True(viewModel.StartDecryptCommand.CanExecute(null));
        Assert.Equal("Ready", viewModel.StatusText);
    }

    [Fact]
    public async Task StartDecryptCommand_WhileRunning_DisablesCommand()
    {
        var workflow = new WaitingWorkflowService();
        var viewModel = CreateReadyViewModel(workflow);

        Task runTask = viewModel.StartDecryptCommand.ExecuteAsync(null);
        await workflow.Started.Task;

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.StartDecryptCommand.CanExecute(null));

        workflow.Finish(new DecryptFileResult("/tmp/out.txt"));
        await runTask;

        Assert.False(viewModel.IsRunning);
        Assert.True(viewModel.StartDecryptCommand.CanExecute(null));
    }

    [Fact]
    public async Task StartDecryptCommand_WhenAuthenticationFails_ShowsGuidance()
    {
        var workflow = new RecordingWorkflowService
        {
            Error = new FileCrypterFormatException(
                FileCrypterFormatErrorCode.AuthenticationFailed,
                "Authentication failed."),
        };
        var viewModel = CreateReadyViewModel(workflow);

        await viewModel.StartDecryptCommand.ExecuteAsync(null);

        Assert.Empty(viewModel.ResultPath);
        Assert.True(viewModel.HasError);
        Assert.Contains("Check the password and key file", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal("Ready", viewModel.StatusText);
    }

    [Fact]
    public async Task BrowseOutputCommand_SuggestsSourceNameWithoutEncryptedSuffix()
    {
        var picker = new RecordingFilePickerService
        {
            SaveResult = "/tmp/plain.txt",
        };
        var viewModel = new DecryptViewModel(new RecordingWorkflowService(), picker)
        {
            SourcePath = "/tmp/plain.txt.encrypted",
            Password = "secret",
        };

        await viewModel.BrowseOutputCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/plain.txt", viewModel.OutputPath);
        Assert.Equal("Choose decrypted output file", picker.LastSaveTitle);
        Assert.Equal("plain.txt", picker.LastSuggestedFileName);
    }

    [Fact]
    public async Task BrowseKeyFileCommand_SetsKeyFilePathAndUpdatesStatus()
    {
        var picker = new RecordingFilePickerService
        {
            OpenResult = "/tmp/matching.key",
        };
        var viewModel = new DecryptViewModel(new RecordingWorkflowService(), picker)
        {
            SourcePath = "/tmp/plain.txt.encrypted",
            Password = "secret",
        };

        await viewModel.BrowseKeyFileCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/matching.key", viewModel.KeyFilePath);
        Assert.True(viewModel.HasKeyFileChoice);
        Assert.Contains("selected key file", viewModel.KeyFileChoiceStatusText, StringComparison.Ordinal);
        Assert.Equal("Choose the matching key file", picker.LastOpenTitle);
    }

    private static DecryptViewModel CreateReadyViewModel(IFileCrypterWorkflowService workflow)
    {
        return new DecryptViewModel(workflow)
        {
            SourcePath = "/tmp/plain.txt.encrypted",
            Password = "secret",
            NeverOverwriteExistingFiles = true,
        };
    }

    private sealed class RecordingWorkflowService : IFileCrypterWorkflowService
    {
        public DecryptFileRequest? Request { get; private set; }

        public DecryptFileResult Result { get; init; } = new("/tmp/out.txt");

        public Exception? Error { get; init; }

        public Task<EncryptFileResult> EncryptFileAsync(
            EncryptFileRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DecryptFileResult> DecryptFileAsync(
            DecryptFileRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            Request = request;
            progress?.Report(new FileCrypterProgress(10, 20) { TotalInputBytes = 10 });

            return Error is null
                ? Task.FromResult(Result)
                : Task.FromException<DecryptFileResult>(Error);
        }
    }

    private sealed class WaitingWorkflowService : IFileCrypterWorkflowService
    {
        private readonly TaskCompletionSource<DecryptFileResult> completion = new();

        public TaskCompletionSource Started { get; } = new();

        public Task<EncryptFileResult> EncryptFileAsync(
            EncryptFileRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<DecryptFileResult> DecryptFileAsync(
            DecryptFileRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            Started.SetResult();
            return completion.Task;
        }

        public void Finish(DecryptFileResult result)
        {
            completion.SetResult(result);
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

        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName, CancellationToken cancellationToken)
        {
            LastSaveTitle = title;
            LastSuggestedFileName = suggestedFileName;
            return Task.FromResult(SaveResult);
        }
    }
}

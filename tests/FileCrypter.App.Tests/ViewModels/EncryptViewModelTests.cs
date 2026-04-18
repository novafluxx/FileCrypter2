using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;
using FileCrypter.Core;

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

        public void Finish(EncryptFileResult result)
        {
            completion.SetResult(result);
        }
    }
}

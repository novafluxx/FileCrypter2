using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;
using FileCrypter.Core;

namespace FileCrypter.App.Tests.ViewModels;

public sealed class BatchViewModelTests
{
    [Fact]
    public void StartBatchCommand_RequiresFilesOutputDirectoryAndPassword()
    {
        var viewModel = new BatchViewModel(new RecordingWorkflowService());

        Assert.False(viewModel.StartBatchCommand.CanExecute(null));

        viewModel.SourcePaths.Add("/tmp/first.txt");
        Assert.False(viewModel.StartBatchCommand.CanExecute(null));

        viewModel.OutputDirectory = "/tmp/out";
        Assert.False(viewModel.StartBatchCommand.CanExecute(null));

        viewModel.Password = "secret";
        Assert.True(viewModel.StartBatchCommand.CanExecute(null));
    }

    [Fact]
    public void StartBatchCommand_InArchiveDecryptMode_RequiresExactlyOneArchive()
    {
        var viewModel = new BatchViewModel(new RecordingWorkflowService())
        {
            ArchiveMode = true,
            EncryptMode = false,
            OutputDirectory = "/tmp/out",
            Password = "secret",
        };

        Assert.False(viewModel.StartBatchCommand.CanExecute(null));

        viewModel.SourcePaths.Add("/tmp/first.tar.zst.encrypted");
        viewModel.SourcePaths.Add("/tmp/second.tar.zst.encrypted");
        Assert.False(viewModel.StartBatchCommand.CanExecute(null));

        viewModel.SourcePaths.RemoveAt(1);
        Assert.True(viewModel.StartBatchCommand.CanExecute(null));
    }

    [Fact]
    public async Task BrowseFilesCommand_AddsFilesAndSkipsDuplicates()
    {
        var picker = new RecordingFilePickerService
        {
            OpenFilesResult =
            [
                "/tmp/first.txt",
                "/tmp/second.txt",
                "/tmp/first.txt",
            ],
        };
        var viewModel = new BatchViewModel(new RecordingWorkflowService(), picker);

        await viewModel.BrowseFilesCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.SourcePaths.Count);
        Assert.Equal("Choose files to encrypt", picker.LastOpenFilesTitle);
    }

    [Fact]
    public async Task BrowseFilesCommand_InArchiveDecryptMode_UsesSingleFilePickerAndReplacesSelection()
    {
        var picker = new RecordingFilePickerService
        {
            OpenFileResult = "/tmp/archive.tar.zst.encrypted",
        };
        var viewModel = new BatchViewModel(new RecordingWorkflowService(), picker)
        {
            ArchiveMode = true,
            EncryptMode = false,
        };
        viewModel.SourcePaths.Add("/tmp/old-first.tar.zst.encrypted");
        viewModel.SourcePaths.Add("/tmp/old-second.tar.zst.encrypted");

        await viewModel.BrowseFilesCommand.ExecuteAsync(null);

        Assert.Single(viewModel.SourcePaths);
        Assert.Equal("/tmp/archive.tar.zst.encrypted", viewModel.SourcePaths[0]);
        Assert.Equal("Choose an encrypted archive to extract", picker.LastOpenTitle);
    }

    [Fact]
    public async Task BrowseOutputDirectoryCommand_SetsOutputDirectory()
    {
        var picker = new RecordingFilePickerService
        {
            FolderResult = "/tmp/output",
        };
        var viewModel = new BatchViewModel(new RecordingWorkflowService(), picker);

        await viewModel.BrowseOutputDirectoryCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/output", viewModel.OutputDirectory);
        Assert.Equal("Choose batch output directory", picker.LastFolderTitle);
    }

    [Fact]
    public async Task StartBatchCommand_WhenSuccessful_ReportsSummaryAndResults()
    {
        var workflow = new RecordingWorkflowService
        {
            BatchResult = new BatchTransformResult(
            [
                new BatchTransformItemResult("/tmp/first.txt", "/tmp/out/first.txt.encrypted", "/tmp/out/first.txt.encrypted", null),
                new BatchTransformItemResult("/tmp/missing.txt", "/tmp/out/missing.txt.encrypted", null, new IOException("Missing input.")),
            ]),
        };
        var viewModel = CreateReadyViewModel(workflow);

        await viewModel.StartBatchCommand.ExecuteAsync(null);

        Assert.NotNull(workflow.BatchRequest);
        Assert.True(workflow.BatchRequest!.NeverOverwriteExistingFiles);
        Assert.Equal(2, viewModel.Results.Count);
        Assert.Contains("failure", viewModel.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Missing input", viewModel.Results.Single(item => !item.Succeeded).DetailText, StringComparison.Ordinal);
        Assert.Equal("Ready", viewModel.StatusText);
        Assert.Equal(100, viewModel.ProgressPercent);
    }

    [Fact]
    public async Task StartBatchCommand_InArchiveEncryptMode_PassesArchiveRequestAndReportsOutput()
    {
        var workflow = new RecordingWorkflowService
        {
            ArchiveEncryptResult = new ArchiveEncryptResult("/tmp/out/filecrypter-archive-20260418-100000.tar.zst.encrypted"),
        };
        var viewModel = CreateReadyViewModel(workflow);
        viewModel.ArchiveMode = true;
        viewModel.ArchiveName = "project-docs";

        await viewModel.StartBatchCommand.ExecuteAsync(null);

        Assert.NotNull(workflow.ArchiveEncryptRequest);
        Assert.Equal("project-docs", workflow.ArchiveEncryptRequest!.ArchiveName);
        Assert.Single(viewModel.Results);
        Assert.Contains("bundled", viewModel.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Ready", viewModel.StatusText);
    }

    [Fact]
    public async Task StartBatchCommand_InArchiveDecryptMode_ReportsExtractedResults()
    {
        var workflow = new RecordingWorkflowService
        {
            ArchiveDecryptResult = new ArchiveDecryptResult(
            [
                "/tmp/out/first.txt",
                "/tmp/out/second.txt",
            ]),
        };
        var viewModel = new BatchViewModel(workflow)
        {
            ArchiveMode = true,
            EncryptMode = false,
            OutputDirectory = "/tmp/out",
            Password = "secret",
            NeverOverwriteExistingFiles = true,
        };
        viewModel.SourcePaths.Add("/tmp/archive.tar.zst.encrypted");

        await viewModel.StartBatchCommand.ExecuteAsync(null);

        Assert.NotNull(workflow.ArchiveDecryptRequest);
        Assert.Equal("/tmp/archive.tar.zst.encrypted", workflow.ArchiveDecryptRequest!.SourcePath);
        Assert.Equal(2, viewModel.Results.Count);
        Assert.All(viewModel.Results, item => Assert.True(item.Succeeded));
        Assert.Contains("extracted", viewModel.ResultSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task StartBatchCommand_WhileRunning_DisablesCommands()
    {
        var workflow = new WaitingWorkflowService();
        var viewModel = CreateReadyViewModel(workflow);

        Task runTask = viewModel.StartBatchCommand.ExecuteAsync(null);
        await workflow.Started.Task;

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.StartBatchCommand.CanExecute(null));
        Assert.False(viewModel.ClearFilesCommand.CanExecute(null));

        workflow.Finish(new BatchTransformResult(
        [
            new BatchTransformItemResult("/tmp/first.txt", "/tmp/out/first.txt.encrypted", "/tmp/out/first.txt.encrypted", null),
        ]));
        await runTask;

        Assert.False(viewModel.IsRunning);
        Assert.True(viewModel.StartBatchCommand.CanExecute(null));
    }

    [Fact]
    public void SwitchingToDecryptMode_UpdatesIdleMessaging()
    {
        var viewModel = new BatchViewModel(new RecordingWorkflowService());

        viewModel.IsDecryptMode = true;

        Assert.False(viewModel.EncryptMode);
        Assert.Contains("decrypt", viewModel.ModeTitle, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("decrypt", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SwitchingToArchiveMode_UpdatesTitlesAndShowsArchiveNameInput()
    {
        var viewModel = new BatchViewModel(new RecordingWorkflowService())
        {
            ArchiveMode = true,
        };

        Assert.Contains("archive", viewModel.ModeTitle, StringComparison.OrdinalIgnoreCase);
        Assert.True(viewModel.ShowArchiveNameEditor);
        Assert.Contains("timestamped", viewModel.ArchiveNameStatusText, StringComparison.OrdinalIgnoreCase);
    }

    private static BatchViewModel CreateReadyViewModel(IFileCrypterWorkflowService workflowService)
    {
        var viewModel = new BatchViewModel(workflowService)
        {
            OutputDirectory = "/tmp/out",
            Password = "secret",
            NeverOverwriteExistingFiles = true,
        };
        viewModel.SourcePaths.Add("/tmp/first.txt");
        viewModel.SourcePaths.Add("/tmp/second.txt");
        return viewModel;
    }

    private sealed class RecordingWorkflowService : IFileCrypterWorkflowService
    {
        public BatchTransformRequest? BatchRequest { get; private set; }

        public ArchiveEncryptRequest? ArchiveEncryptRequest { get; private set; }

        public ArchiveDecryptRequest? ArchiveDecryptRequest { get; private set; }

        public BatchTransformResult BatchResult { get; init; } = new([]);

        public ArchiveEncryptResult ArchiveEncryptResult { get; init; } = new("/tmp/out/archive.tar.zst.encrypted");

        public ArchiveDecryptResult ArchiveDecryptResult { get; init; } = new([]);

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
            throw new NotSupportedException();
        }

        public Task<ArchiveEncryptResult> EncryptArchiveAsync(
            ArchiveEncryptRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            ArchiveEncryptRequest = request;
            progress?.Report(new FileCrypterProgress(10, 20)
            {
                Phase = FileCrypterProgressPhases.CreatingArchive,
                TotalInputBytes = 20,
            });

            return Error is null
                ? Task.FromResult(ArchiveEncryptResult)
                : Task.FromException<ArchiveEncryptResult>(Error);
        }

        public Task<ArchiveDecryptResult> DecryptArchiveAsync(
            ArchiveDecryptRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            ArchiveDecryptRequest = request;
            progress?.Report(new FileCrypterProgress(10, 20)
            {
                Phase = FileCrypterProgressPhases.DecryptingArchive,
                TotalInputBytes = 20,
            });

            return Error is null
                ? Task.FromResult(ArchiveDecryptResult)
                : Task.FromException<ArchiveDecryptResult>(Error);
        }

        public Task<BatchTransformResult> EncryptFilesAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            return CompleteBatchAsync(request, progress);
        }

        public Task<BatchTransformResult> DecryptFilesAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            return CompleteBatchAsync(request, progress);
        }

        private Task<BatchTransformResult> CompleteBatchAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress)
        {
            BatchRequest = request;
            progress?.Report(new BatchOperationProgress(0, request.SourcePaths.Count, request.SourcePaths[0], 10, 20, 25));

            return Error is null
                ? Task.FromResult(BatchResult)
                : Task.FromException<BatchTransformResult>(Error);
        }
    }

    private sealed class WaitingWorkflowService : IFileCrypterWorkflowService
    {
        private readonly TaskCompletionSource<BatchTransformResult> completion = new();

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
            Started.SetResult();
            return completion.Task;
        }

        public Task<BatchTransformResult> DecryptFilesAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            Started.SetResult();
            return completion.Task;
        }

        public void Finish(BatchTransformResult result)
        {
            completion.SetResult(result);
        }
    }

    private sealed class RecordingFilePickerService : IFilePickerService
    {
        public IReadOnlyList<string> OpenFilesResult { get; init; } = [];

        public string? OpenFileResult { get; init; }

        public string? FolderResult { get; init; }

        public string? SaveResult { get; init; }

        public string? LastOpenTitle { get; private set; }

        public string? LastOpenFilesTitle { get; private set; }

        public string? LastFolderTitle { get; private set; }

        public string? LastSaveTitle { get; private set; }

        public string? LastSuggestedFileName { get; private set; }

        public Task<string?> PickOpenFileAsync(string title, CancellationToken cancellationToken)
        {
            LastOpenTitle = title;
            return Task.FromResult(OpenFileResult);
        }

        public Task<IReadOnlyList<string>> PickOpenFilesAsync(string title, CancellationToken cancellationToken)
        {
            LastOpenFilesTitle = title;
            return Task.FromResult(OpenFilesResult);
        }

        public Task<string?> PickOpenFolderAsync(string title, CancellationToken cancellationToken)
        {
            LastFolderTitle = title;
            return Task.FromResult(FolderResult);
        }

        public Task<string?> PickSaveFileAsync(string title, string? suggestedFileName, CancellationToken cancellationToken)
        {
            LastSaveTitle = title;
            LastSuggestedFileName = suggestedFileName;
            return Task.FromResult(SaveResult);
        }
    }
}

using System.IO;
using FileCrypter.Desktop.Services;
using FileCrypter.Desktop.Tests.TestDoubles;
using FileCrypter.Desktop.ViewModels;
using FileCrypter.Core;
using FileCrypter.Core.Format;
using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.Tests.ViewModels;

public sealed class DecryptViewModelTests
{
    [Fact]
    public void StartDecryptCommand_RequiresSourcePathAndPassword()
    {
        var viewModel = new DecryptViewModel(new RecordingWorkflowService());

        Assert.False(viewModel.StartDecryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);
        Assert.False(viewModel.HasError);

        viewModel.SourcePath = "/tmp/plain.txt.encrypted";
        Assert.False(viewModel.StartDecryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);

        viewModel.Password = "secret";
        Assert.True(viewModel.StartDecryptCommand.CanExecute(null));
        Assert.True(viewModel.ShowReadyAction);
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
        Assert.Empty(viewModel.Password);
        Assert.Empty(viewModel.ErrorMessage);
        Assert.False(viewModel.StartDecryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);
        Assert.StartsWith("Decrypted in ", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal("Saved to", viewModel.ProgressText);
        Assert.Equal("/tmp/plain.txt", viewModel.FooterActionText);
        Assert.True(viewModel.HasFooterAction);
    }

    [Fact]
    public async Task StartDecryptCommand_WhileRunning_DisablesCommand()
    {
        var workflow = new WaitingWorkflowService();
        var viewModel = CreateReadyViewModel(workflow);

        Task runTask = viewModel.StartDecryptCommand.ExecuteAsync(null);
        await workflow.Started.Task;

        Assert.True(viewModel.IsRunning);
        Assert.Empty(viewModel.Password);
        Assert.NotNull(workflow.Request);
        Assert.Equal("secret", workflow.Request.Password);
        Assert.False(viewModel.StartDecryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);

        workflow.Finish(new DecryptFileResult("/tmp/out.txt"));
        await runTask;

        Assert.False(viewModel.IsRunning);
        Assert.False(viewModel.StartDecryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);
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
        Assert.Empty(viewModel.Password);
        Assert.True(viewModel.HasError);
        Assert.Contains("Check the password and key file", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal("Decryption failed", viewModel.StatusText);
        Assert.Contains("Check the password and key file", viewModel.ProgressText, StringComparison.Ordinal);
        Assert.False(viewModel.StartDecryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);
    }

    [Fact]
    public async Task ChangingInputsAfterFailure_DismissesVisibleError()
    {
        var workflow = new RecordingWorkflowService
        {
            Error = new FileCrypterFormatException(
                FileCrypterFormatErrorCode.AuthenticationFailed,
                "Authentication failed."),
        };
        var viewModel = CreateReadyViewModel(workflow);

        await viewModel.StartDecryptCommand.ExecuteAsync(null);
        Assert.True(viewModel.HasError);

        viewModel.Password = TestPasswordSamples.Changed;

        Assert.False(viewModel.HasError);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CopyErrorCommand_CopiesTroubleshootingMessage()
    {
        var clipboard = new RecordingClipboardService();
        var workflow = new RecordingWorkflowService
        {
            Error = new FileCrypterFormatException(
                FileCrypterFormatErrorCode.AuthenticationFailed,
                "Authentication failed."),
        };
        var viewModel = new DecryptViewModel(workflow, filePickerService: null, clipboardService: clipboard)
        {
            SourcePath = "/tmp/plain.txt.encrypted",
            Password = "secret",
        };
        WorkflowToastNotification? toast = null;
        viewModel.ToastNotificationRequested += (_, notification) => toast = notification;

        await viewModel.StartDecryptCommand.ExecuteAsync(null);
        string progressTextAfterFailure = viewModel.ProgressText;
        await viewModel.CopyErrorCommand.ExecuteAsync(null);

        Assert.NotNull(clipboard.LastText);
        Assert.Contains("Check the password and key file", clipboard.LastText, StringComparison.Ordinal);
        Assert.Equal(progressTextAfterFailure, viewModel.ProgressText);
        Assert.NotNull(toast);
        Assert.Equal("Copied", toast.Title);
        Assert.Equal("Copied issue details.", toast.Message);
    }

    [Fact]
    public async Task RevealFooterPathCommand_RevealsSuccessfulOutputPath()
    {
        var revealService = new RecordingPathRevealService();
        var workflow = new RecordingWorkflowService
        {
            Result = new DecryptFileResult("/tmp/plain.txt"),
        };
        var viewModel = new DecryptViewModel(
            workflow,
            filePickerService: null,
            clipboardService: null,
            pathRevealService: revealService)
        {
            SourcePath = "/tmp/plain.txt.encrypted",
            Password = "secret",
        };

        await viewModel.StartDecryptCommand.ExecuteAsync(null);
        await viewModel.RevealFooterPathCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/plain.txt", revealService.LastPath);
    }

    [Fact]
    public async Task RevealFooterPathCommand_WhenRevealFails_ShowsSanitizedWarningToast()
    {
        var revealService = new RecordingPathRevealService
        {
            Result = false,
        };
        var workflow = new RecordingWorkflowService
        {
            Result = new DecryptFileResult("/tmp/plain.txt"),
        };
        var viewModel = new DecryptViewModel(
            workflow,
            filePickerService: null,
            clipboardService: null,
            pathRevealService: revealService)
        {
            SourcePath = "/tmp/plain.txt.encrypted",
            Password = "secret",
        };
        WorkflowToastNotification? toast = null;
        viewModel.ToastNotificationRequested += (_, notification) => toast = notification;

        await viewModel.StartDecryptCommand.ExecuteAsync(null);
        await viewModel.RevealFooterPathCommand.ExecuteAsync(null);

        Assert.Equal("Could not reveal that path.", viewModel.ProgressText);
        Assert.NotNull(toast);
        Assert.Equal(WorkflowToastKind.Warning, toast.Kind);
        Assert.Equal("Could not reveal that path.", toast.Message);
    }

    [Fact]
    public async Task BrowseKeyFileCommand_WhenPickerFails_ShowsSanitizedStatusAndToast()
    {
        var picker = new RecordingFilePickerService
        {
            OpenException = new InvalidOperationException("native picker crashed at C:/secret/key.file"),
        };
        var viewModel = new DecryptViewModel(new RecordingWorkflowService(), picker)
        {
            SourcePath = "/tmp/plain.txt.encrypted",
            Password = "secret",
        };
        WorkflowToastNotification? toast = null;
        viewModel.ToastNotificationRequested += (_, notification) => toast = notification;

        await viewModel.BrowseKeyFileCommand.ExecuteAsync(null);

        Assert.Equal("Could not open the file picker.", viewModel.ProgressText);
        Assert.DoesNotContain("secret", viewModel.ProgressText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(toast);
        Assert.Equal(WorkflowToastKind.Warning, toast.Kind);
        Assert.Equal("Could not open the file picker.", toast.Message);
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

    [Fact]
    public void Constructor_WithSharedSettings_AppliesOverwriteAndDefaultOutputDirectory()
    {
        using var outputDirectory = new TemporaryDirectory();
        var viewModel = new DecryptViewModel(
            new RecordingWorkflowService(),
            new FileCrypterSettings
            {
                NeverOverwriteExistingFilesByDefault = false,
                DefaultOutputDirectory = outputDirectory.Path,
            });

        viewModel.SourcePath = "/tmp/plain.txt.encrypted";

        Assert.False(viewModel.NeverOverwriteExistingFiles);
        Assert.Equal(Path.Combine(outputDirectory.Path, "plain.txt"), viewModel.OutputPath);
    }

    [Fact]
    public void ApplyDroppedSourcePaths_UsesFirstDroppedFileAndRefreshesSuggestedOutput()
    {
        using var outputDirectory = new TemporaryDirectory();
        var viewModel = new DecryptViewModel(
            new RecordingWorkflowService(),
            new FileCrypterSettings
            {
                DefaultOutputDirectory = outputDirectory.Path,
            });

        bool applied = viewModel.ApplyDroppedSourcePaths(["/tmp/dropped.txt.encrypted", "/tmp/other.txt.encrypted"]);

        Assert.True(applied);
        Assert.Equal(Path.GetFullPath("/tmp/dropped.txt.encrypted"), viewModel.SourcePath);
        Assert.Equal(Path.Combine(outputDirectory.Path, "dropped.txt"), viewModel.OutputPath);
    }

    [Fact]
    public void SourcePreview_ShowsMetadataAndClearCommandRestoresEmptyState()
    {
        string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt.encrypted");
        File.WriteAllText(tempFilePath, "encrypted!");

        try
        {
            var viewModel = new DecryptViewModel(new RecordingWorkflowService())
            {
                SourcePath = tempFilePath,
            };

            Assert.NotNull(viewModel.SourcePreview);
            Assert.Equal(Path.GetFullPath(tempFilePath), viewModel.SourcePreview!.FullPath);
            Assert.Equal("10 B", viewModel.SourcePreview.SizeText);
            Assert.False(viewModel.ShowEmptySourceState);
            Assert.Equal($"{Path.GetFileName(tempFilePath)} - 10 B", viewModel.ProgressText);

            viewModel.ClearSourceCommand.Execute(null);

            Assert.Empty(viewModel.SourcePath);
            Assert.Null(viewModel.SourcePreview);
            Assert.True(viewModel.ShowEmptySourceState);
        }
        finally
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
            }
        }
    }

    [Fact]
    public void TogglePasswordVisibilityCommand_TogglesVisiblePasswordState()
    {
        var viewModel = new DecryptViewModel(new RecordingWorkflowService());

        Assert.True(viewModel.ShowMaskedPasswordInput);
        Assert.Equal("Show", viewModel.PasswordVisibilityActionText);

        viewModel.TogglePasswordVisibilityCommand.Execute(null);

        Assert.False(viewModel.ShowMaskedPasswordInput);
        Assert.True(viewModel.ShowPassword);
        Assert.Equal("Hide", viewModel.PasswordVisibilityActionText);
    }

    [Fact]
    public void CancelCommand_WhileIdle_IsDisabled()
    {
        var viewModel = CreateReadyViewModel(new RecordingWorkflowService());

        Assert.False(viewModel.CancelCommand.CanExecute(null));
    }

    [Fact]
    public async Task CancelCommand_WhileRunning_IsEnabledAndCancelsTokenPassedToWorkflow()
    {
        var workflow = new CancellableDecryptWorkflowService();
        var viewModel = CreateReadyViewModel(workflow);

        Task runTask = viewModel.StartDecryptCommand.ExecuteAsync(null);
        await workflow.Started.Task;

        Assert.True(viewModel.IsRunning);
        Assert.True(viewModel.CancelCommand.CanExecute(null));
        Assert.False(workflow.ReceivedToken.IsCancellationRequested);

        viewModel.CancelCommand.Execute(null);

        Assert.True(workflow.ReceivedToken.IsCancellationRequested);
        await runTask;

        Assert.False(viewModel.IsRunning);
    }

    [Fact]
    public async Task StartDecryptCommand_WhenCancelled_SetsCancelledStatusAndClearsPasswordWithoutError()
    {
        var workflow = new CancellableDecryptWorkflowService();
        var viewModel = CreateReadyViewModel(workflow);

        Task runTask = viewModel.StartDecryptCommand.ExecuteAsync(null);
        await workflow.Started.Task;
        viewModel.CancelCommand.Execute(null);
        await runTask;

        Assert.False(viewModel.IsRunning);
        Assert.Empty(viewModel.Password);
        Assert.False(viewModel.HasError);
        Assert.Empty(viewModel.VisibleErrorMessage);
        Assert.Equal("Cancelled", viewModel.StatusText);
        Assert.Equal("Decryption cancelled.", viewModel.ProgressText);
        Assert.Empty(viewModel.FooterActionText);
        Assert.False(viewModel.HasResult);
        Assert.False(viewModel.CancelCommand.CanExecute(null));
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
        private readonly TaskCompletionSource<DecryptFileResult> completion = new();

        public TaskCompletionSource Started { get; } = new();

        public DecryptFileRequest? Request { get; private set; }

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
            Started.SetResult();
            return completion.Task;
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

        public void Finish(DecryptFileResult result)
        {
            completion.SetResult(result);
        }
    }

    private sealed class CancellableDecryptWorkflowService : IFileCrypterWorkflowService
    {
        private readonly TaskCompletionSource<DecryptFileResult> completion = new();

        public TaskCompletionSource Started { get; } = new();

        public CancellationToken ReceivedToken { get; private set; }

        public DecryptFileRequest? Request { get; private set; }

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
            ReceivedToken = cancellationToken;
            Started.SetResult();
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
            return completion.Task;
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

        public Exception? OpenException { get; init; }

        public string? LastOpenTitle { get; private set; }

        public string? LastSaveTitle { get; private set; }

        public string? LastSuggestedFileName { get; private set; }

        public Task<string?> PickOpenFileAsync(string title, CancellationToken cancellationToken)
        {
            LastOpenTitle = title;
            if (OpenException is not null)
            {
                return Task.FromException<string?>(OpenException);
            }

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

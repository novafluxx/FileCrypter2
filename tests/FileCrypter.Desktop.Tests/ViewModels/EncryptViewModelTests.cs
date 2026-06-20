using FileCrypter.Desktop.Services;
using FileCrypter.Desktop.Tests.TestDoubles;
using FileCrypter.Desktop.ViewModels;
using FileCrypter.Core;
using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.Tests.ViewModels;

public sealed class EncryptViewModelTests
{
    [Fact]
    public void StartEncryptCommand_RequiresSourcePathAndPassword()
    {
        var viewModel = new EncryptViewModel(new RecordingWorkflowService());

        Assert.False(viewModel.StartEncryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);
        Assert.False(viewModel.HasError);

        viewModel.SourcePath = "/tmp/plain.txt";
        Assert.False(viewModel.StartEncryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);

        viewModel.Password = "secret";
        Assert.True(viewModel.StartEncryptCommand.CanExecute(null));
        Assert.True(viewModel.ShowReadyAction);
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
        Assert.Empty(viewModel.Password);
        Assert.Empty(viewModel.ErrorMessage);
        Assert.False(viewModel.StartEncryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);
        Assert.StartsWith("Encrypted in ", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal("Saved to", viewModel.ProgressText);
        Assert.Equal("/tmp/plain.txt.encrypted", viewModel.FooterActionText);
        Assert.True(viewModel.HasFooterAction);
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
        Assert.Empty(viewModel.Password);
        Assert.NotNull(workflow.Request);
        Assert.Equal("secret", workflow.Request.Password);
        Assert.False(viewModel.StartEncryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);

        workflow.Finish(new EncryptFileResult("/tmp/out.encrypted", null));
        await runTask;

        Assert.False(viewModel.IsRunning);
        Assert.False(viewModel.StartEncryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);
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
        Assert.Empty(viewModel.Password);
        Assert.True(viewModel.HasError);
        Assert.Contains("could not access one of the selected paths", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.DoesNotContain("output directory is missing", viewModel.ErrorMessage, StringComparison.Ordinal);
        Assert.Equal("Encryption failed", viewModel.StatusText);
        Assert.Contains("could not access one of the selected paths", viewModel.ProgressText, StringComparison.Ordinal);
        Assert.False(viewModel.StartEncryptCommand.CanExecute(null));
        Assert.False(viewModel.ShowReadyAction);
    }

    [Fact]
    public async Task ChangingInputsAfterFailure_DismissesVisibleError()
    {
        var workflow = new RecordingWorkflowService
        {
            Error = new IOException("The output directory is missing."),
        };
        var viewModel = CreateReadyViewModel(workflow);

        await viewModel.StartEncryptCommand.ExecuteAsync(null);
        Assert.True(viewModel.HasError);

        viewModel.Password = TestPasswordSamples.Changed;

        Assert.False(viewModel.HasError);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public async Task CopyResultCommand_CopiesEncryptedAndGeneratedKeyFilePaths()
    {
        var clipboard = new RecordingClipboardService();
        var workflow = new RecordingWorkflowService
        {
            Result = new EncryptFileResult("/tmp/plain.txt.encrypted", "/tmp/plain.key"),
        };
        var viewModel = new EncryptViewModel(workflow, filePickerService: null, clipboardService: clipboard)
        {
            SourcePath = "/tmp/plain.txt",
            Password = "secret",
        };
        WorkflowToastNotification? toast = null;
        viewModel.ToastNotificationRequested += (_, notification) => toast = notification;

        await viewModel.StartEncryptCommand.ExecuteAsync(null);
        string progressTextAfterRun = viewModel.ProgressText;
        await viewModel.CopyResultCommand.ExecuteAsync(null);

        Assert.NotNull(clipboard.LastText);
        Assert.Contains("Encryption complete.", clipboard.LastText, StringComparison.Ordinal);
        Assert.Contains("Encrypted file: /tmp/plain.txt.encrypted", clipboard.LastText, StringComparison.Ordinal);
        Assert.Contains("Generated key file: /tmp/plain.key", clipboard.LastText, StringComparison.Ordinal);
        Assert.Equal(progressTextAfterRun, viewModel.ProgressText);
        Assert.NotNull(toast);
        Assert.Equal("Copied", toast.Title);
        Assert.Equal("Copied result details.", toast.Message);
    }

    [Fact]
    public async Task RevealFooterPathCommand_RevealsSuccessfulOutputPath()
    {
        var revealService = new RecordingPathRevealService();
        var workflow = new RecordingWorkflowService
        {
            Result = new EncryptFileResult("/tmp/plain.txt.encrypted", null),
        };
        var viewModel = new EncryptViewModel(
            workflow,
            filePickerService: null,
            clipboardService: null,
            pathRevealService: revealService)
        {
            SourcePath = "/tmp/plain.txt",
            Password = "secret",
        };

        await viewModel.StartEncryptCommand.ExecuteAsync(null);
        await viewModel.RevealFooterPathCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/plain.txt.encrypted", revealService.LastPath);
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
            Result = new EncryptFileResult("/tmp/plain.txt.encrypted", null),
        };
        var viewModel = new EncryptViewModel(
            workflow,
            filePickerService: null,
            clipboardService: null,
            pathRevealService: revealService)
        {
            SourcePath = "/tmp/plain.txt",
            Password = "secret",
        };
        WorkflowToastNotification? toast = null;
        viewModel.ToastNotificationRequested += (_, notification) => toast = notification;

        await viewModel.StartEncryptCommand.ExecuteAsync(null);
        await viewModel.RevealFooterPathCommand.ExecuteAsync(null);

        Assert.Equal("Could not reveal that path.", viewModel.ProgressText);
        Assert.NotNull(toast);
        Assert.Equal(WorkflowToastKind.Warning, toast.Kind);
        Assert.Equal("Could not reveal that path.", toast.Message);
    }

    [Fact]
    public async Task BrowseSourceCommand_WhenPickerFails_ShowsSanitizedStatusAndToast()
    {
        var picker = new RecordingFilePickerService
        {
            OpenException = new InvalidOperationException("native picker crashed at C:/secret/plain.txt"),
        };
        var viewModel = new EncryptViewModel(new RecordingWorkflowService(), picker);
        WorkflowToastNotification? toast = null;
        viewModel.ToastNotificationRequested += (_, notification) => toast = notification;

        await viewModel.BrowseSourceCommand.ExecuteAsync(null);

        Assert.Equal("Could not open the file picker.", viewModel.ProgressText);
        Assert.DoesNotContain("secret", viewModel.ProgressText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(toast);
        Assert.Equal(WorkflowToastKind.Warning, toast.Kind);
        Assert.Equal("Could not open the file picker.", toast.Message);
    }

    [Fact]
    public async Task CopyErrorCommand_WhenClipboardFails_ShowsSanitizedWarning()
    {
        var clipboard = new RecordingClipboardService
        {
            SetTextException = new InvalidOperationException("clipboard denied C:/secret/plain.txt"),
        };
        var workflow = new RecordingWorkflowService
        {
            Error = new IOException("The output directory is missing."),
        };
        var viewModel = new EncryptViewModel(workflow, filePickerService: null, clipboardService: clipboard)
        {
            SourcePath = "/tmp/plain.txt",
            Password = "secret",
        };
        WorkflowToastNotification? toast = null;
        viewModel.ToastNotificationRequested += (_, notification) => toast = notification;

        await viewModel.StartEncryptCommand.ExecuteAsync(null);
        await viewModel.CopyErrorCommand.ExecuteAsync(null);

        Assert.Equal("Clipboard copy failed.", viewModel.ProgressText);
        Assert.DoesNotContain("secret", viewModel.ProgressText, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(toast);
        Assert.Equal(WorkflowToastKind.Warning, toast.Kind);
        Assert.Equal("Clipboard copy failed.", toast.Message);
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
    public async Task GenerateKeyFileCommand_ChoosesGeneratedKeyFilePath()
    {
        var picker = new RecordingFilePickerService
        {
            SaveResult = "/tmp/generated.key",
        };
        var viewModel = new EncryptViewModel(new RecordingWorkflowService(), picker)
        {
            SourcePath = "/tmp/plain.txt",
            Password = "secret",
        };

        await viewModel.GenerateKeyFileCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/generated.key", viewModel.GenerateKeyFilePath);
        Assert.Equal("Choose where to save a new key file", picker.LastSaveTitle);
        Assert.Equal("filecrypter.key", picker.LastSuggestedFileName);
        Assert.Contains("new key file", viewModel.KeyFileChoiceStatusText, StringComparison.Ordinal);
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

    [Fact]
    public void ApplyDroppedSourcePaths_UsesFirstDroppedFileAndRefreshesSuggestedOutput()
    {
        using var outputDirectory = new TemporaryDirectory();
        var viewModel = new EncryptViewModel(
            new RecordingWorkflowService(),
            new FileCrypterSettings
            {
                DefaultOutputDirectory = outputDirectory.Path,
            });

        bool applied = viewModel.ApplyDroppedSourcePaths(["/tmp/dropped.txt", "/tmp/other.txt"]);

        Assert.True(applied);
        Assert.Equal(Path.GetFullPath("/tmp/dropped.txt"), viewModel.SourcePath);
        Assert.Equal(Path.Combine(outputDirectory.Path, "dropped.txt.encrypted"), viewModel.OutputPath);
    }

    [Fact]
    public void SourcePreview_ShowsMetadataAndClearCommandRestoresEmptyState()
    {
        string tempFilePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.txt");
        File.WriteAllText(tempFilePath, "hello world");

        try
        {
            var viewModel = new EncryptViewModel(new RecordingWorkflowService())
            {
                SourcePath = tempFilePath,
            };

            Assert.NotNull(viewModel.SourcePreview);
            Assert.Equal(Path.GetFullPath(tempFilePath), viewModel.SourcePreview!.FullPath);
            Assert.Equal("11 B", viewModel.SourcePreview.SizeText);
            Assert.False(viewModel.ShowEmptySourceState);
            Assert.Equal($"{Path.GetFileName(tempFilePath)} - 11 B", viewModel.ProgressText);

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
        var viewModel = new EncryptViewModel(new RecordingWorkflowService());

        Assert.True(viewModel.ShowMaskedPasswordInput);
        Assert.Equal("Show", viewModel.PasswordVisibilityActionText);

        viewModel.TogglePasswordVisibilityCommand.Execute(null);

        Assert.False(viewModel.ShowMaskedPasswordInput);
        Assert.True(viewModel.ShowPassword);
        Assert.Equal("Hide", viewModel.PasswordVisibilityActionText);
    }

    [Fact]
    public void PasswordStrengthProperties_FollowPasswordComplexity()
    {
        var viewModel = new EncryptViewModel(new RecordingWorkflowService())
        {
            Password = TestPasswordSamples.Strong,
        };

        Assert.Equal(4, viewModel.PasswordStrengthScore);
        Assert.Equal(100d, viewModel.PasswordStrengthPercent);
        Assert.Equal("Excellent", viewModel.PasswordStrengthLabel);
        Assert.Contains("bits of estimated entropy", viewModel.PasswordStrengthDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void PasswordStrengthProperties_UpdateWhenPasswordChanges()
    {
        var viewModel = new EncryptViewModel(new RecordingWorkflowService())
        {
            Password = "short",
        };

        Assert.Equal(0, viewModel.PasswordStrengthScore);
        Assert.Equal("Enter a passphrase", viewModel.PasswordStrengthLabel);

        viewModel.Password = TestPasswordSamples.Strong;

        Assert.Equal(4, viewModel.PasswordStrengthScore);
        Assert.Equal(100d, viewModel.PasswordStrengthPercent);
        Assert.Equal("Excellent", viewModel.PasswordStrengthLabel);
        Assert.Contains("bits of estimated entropy", viewModel.PasswordStrengthDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void GenerateRandomPasswordCommand_FillsPasswordShowsItAndEnablesEncrypt()
    {
        var generator = new RecordingPasswordGeneratorService
        {
            RandomPassword = TestPasswordSamples.GeneratedRandom,
        };
        var viewModel = new EncryptViewModel(new RecordingWorkflowService(), passwordGeneratorService: generator)
        {
            SourcePath = "/tmp/plain.txt",
        };

        viewModel.GenerateRandomPasswordCommand.Execute(null);

        Assert.Equal(TestPasswordSamples.GeneratedRandom, viewModel.Password);
        Assert.True(viewModel.ShowPassword);
        Assert.False(viewModel.ShowMaskedPasswordInput);
        Assert.True(viewModel.StartEncryptCommand.CanExecute(null));
        Assert.Equal(4, viewModel.PasswordStrengthScore);
    }

    [Fact]
    public void GenerateMemorablePassphraseCommand_FillsPasswordShowsItAndEnablesEncrypt()
    {
        var generator = new RecordingPasswordGeneratorService
        {
            MemorablePassphrase = TestPasswordSamples.MemorablePassphrase,
        };
        var viewModel = new EncryptViewModel(new RecordingWorkflowService(), passwordGeneratorService: generator)
        {
            SourcePath = "/tmp/plain.txt",
        };

        viewModel.GenerateMemorablePassphraseCommand.Execute(null);

        Assert.Equal(TestPasswordSamples.MemorablePassphrase, viewModel.Password);
        Assert.True(viewModel.ShowPassword);
        Assert.False(viewModel.ShowMaskedPasswordInput);
        Assert.True(viewModel.StartEncryptCommand.CanExecute(null));
        Assert.Equal(4, viewModel.PasswordStrengthScore);
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
        var workflow = new CancellableEncryptWorkflowService();
        var viewModel = CreateReadyViewModel(workflow);

        Task runTask = viewModel.StartEncryptCommand.ExecuteAsync(null);
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
    public async Task StartEncryptCommand_WhenCancelled_SetsCancelledStatusAndClearsPasswordWithoutError()
    {
        var workflow = new CancellableEncryptWorkflowService();
        var viewModel = CreateReadyViewModel(workflow);

        Task runTask = viewModel.StartEncryptCommand.ExecuteAsync(null);
        await workflow.Started.Task;
        viewModel.CancelCommand.Execute(null);
        await runTask;

        Assert.False(viewModel.IsRunning);
        Assert.Empty(viewModel.Password);
        Assert.False(viewModel.HasError);
        Assert.Empty(viewModel.VisibleErrorMessage);
        Assert.Equal("Cancelled", viewModel.StatusText);
        Assert.Equal("Encryption cancelled.", viewModel.ProgressText);
        Assert.Empty(viewModel.FooterActionText);
        Assert.False(viewModel.HasResult);
        Assert.False(viewModel.CancelCommand.CanExecute(null));
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

        public EncryptFileRequest? Request { get; private set; }

        public Task<EncryptFileResult> EncryptFileAsync(
            EncryptFileRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            Request = request;
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

    private sealed class CancellableEncryptWorkflowService : IFileCrypterWorkflowService
    {
        private readonly TaskCompletionSource<EncryptFileResult> completion = new();

        public TaskCompletionSource Started { get; } = new();

        public CancellationToken ReceivedToken { get; private set; }

        public EncryptFileRequest? Request { get; private set; }

        public Task<EncryptFileResult> EncryptFileAsync(
            EncryptFileRequest request,
            IProgress<FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            Request = request;
            ReceivedToken = cancellationToken;
            Started.SetResult();
            cancellationToken.Register(() => completion.TrySetCanceled(cancellationToken));
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
    }

    private sealed class RecordingPasswordGeneratorService : IPasswordGeneratorService
    {
        public string RandomPassword { get; init; } = TestPasswordSamples.GeneratedRandom;

        public string MemorablePassphrase { get; init; } = TestPasswordSamples.MemorablePassphrase;

        public string GenerateRandomPassword()
        {
            return RandomPassword;
        }

        public string GenerateMemorablePassphrase()
        {
            return MemorablePassphrase;
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

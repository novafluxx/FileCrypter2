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

    [Fact]
    public void SourcePath_WhenInspectionSucceeds_ShowsInspectedPayloadDetails()
    {
        var workflow = new RecordingWorkflowService
        {
            InspectResult = new InspectFileResult(
                FileCrypterPayloadKind.SingleFile,
                IsKeyFileRequired: false,
                IsCompressed: true,
                FormatVersion: 1),
        };
        var viewModel = new DecryptViewModel(workflow);

        viewModel.SourcePath = "/tmp/plain.txt.encrypted";

        Assert.Equal(1, workflow.InspectCallCount);
        Assert.NotNull(workflow.InspectRequest);
        Assert.Equal("/tmp/plain.txt.encrypted", workflow.InspectRequest!.SourcePath);
        Assert.True(viewModel.HasInspectedFile);
        Assert.Equal("FILE DETAILS", viewModel.DetailsPanelTitle);
        Assert.Contains("is a FileCrypter encrypted file.", viewModel.DetailsPanelBody, StringComparison.Ordinal);
        Assert.Contains("no key file is required", viewModel.DetailsPanelDetailText, StringComparison.Ordinal);
        Assert.Contains("payload is compressed", viewModel.DetailsPanelDetailText, StringComparison.Ordinal);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void SourcePath_WhenInspectedPayloadRequiresKeyFile_MakesKeyFileStatusDefinitive()
    {
        var workflow = new RecordingWorkflowService
        {
            InspectResult = new InspectFileResult(
                FileCrypterPayloadKind.SingleFile,
                IsKeyFileRequired: true,
                IsCompressed: false,
                FormatVersion: 1),
        };
        var viewModel = new DecryptViewModel(workflow);

        viewModel.SourcePath = "/tmp/plain.txt.encrypted";

        Assert.Equal(
            "This file requires a key file. Select the matching key file below to decrypt it.",
            viewModel.KeyFileChoiceStatusText);
        Assert.DoesNotContain("unless", viewModel.KeyFileChoiceStatusText, StringComparison.Ordinal);
        Assert.Contains("requires the matching key file", viewModel.DetailsPanelDetailText, StringComparison.Ordinal);

        viewModel.KeyFilePath = "/tmp/matching.key";

        Assert.Equal(
            "This file requires a key file. The selected key file will be used with the password.",
            viewModel.KeyFileChoiceStatusText);
    }

    [Fact]
    public void SourcePath_WhenInspectedPayloadIsArchive_SteersUserToBatchArchiveMode()
    {
        var workflow = new RecordingWorkflowService
        {
            InspectResult = new InspectFileResult(
                FileCrypterPayloadKind.TarArchive,
                IsKeyFileRequired: false,
                IsCompressed: true,
                FormatVersion: 1),
        };
        var viewModel = new DecryptViewModel(workflow);

        viewModel.SourcePath = "/tmp/bundle.tar.zst.encrypted";

        Assert.Contains("is a FileCrypter encrypted archive.", viewModel.DetailsPanelBody, StringComparison.Ordinal);
        Assert.Contains("Batch page", viewModel.DetailsPanelDetailText, StringComparison.Ordinal);
        Assert.Contains("archive mode", viewModel.DetailsPanelDetailText, StringComparison.Ordinal);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void SourcePath_WhenFileIsNotFileCrypterFormat_ShowsCalmMessageWithoutErrorState()
    {
        var workflow = new RecordingWorkflowService
        {
            InspectError = new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidMagic,
                "The header magic bytes are not FileCrypter."),
        };
        var viewModel = new DecryptViewModel(workflow);

        viewModel.SourcePath = "/tmp/holiday-photo.jpg";

        Assert.False(viewModel.HasInspectedFile);
        Assert.False(viewModel.HasError);
        Assert.Empty(viewModel.VisibleErrorMessage);
        Assert.Empty(viewModel.ErrorMessage);
        Assert.Equal("Ready", viewModel.StatusText);
        Assert.Contains("is not a FileCrypter encrypted file.", viewModel.DetailsPanelBody, StringComparison.Ordinal);
        Assert.Contains("not encrypted by FileCrypter", viewModel.DetailsPanelDetailText, StringComparison.Ordinal);
        Assert.DoesNotContain("Exception", viewModel.DetailsPanelDetailText, StringComparison.Ordinal);
    }

    [Fact]
    public void SourcePath_WhenSecondSelectionInspectsFirst_IgnoresSlowerEarlierInspection()
    {
        PumpingSynchronizationContext.Run(async () =>
        {
            var workflow = new DeferredInspectWorkflowService();
            var viewModel = new DecryptViewModel(workflow);

            viewModel.SourcePath = "/tmp/first.txt.encrypted";
            viewModel.SourcePath = "/tmp/second.tar.zst.encrypted";

            workflow.Finish(
                "/tmp/second.tar.zst.encrypted",
                new InspectFileResult(
                    FileCrypterPayloadKind.TarArchive,
                    IsKeyFileRequired: false,
                    IsCompressed: true,
                    FormatVersion: 1));
            await Task.Yield();

            workflow.Finish(
                "/tmp/first.txt.encrypted",
                new InspectFileResult(
                    FileCrypterPayloadKind.SingleFile,
                    IsKeyFileRequired: true,
                    IsCompressed: false,
                    FormatVersion: 1));
            await Task.Yield();

            Assert.Contains("second.tar.zst.encrypted", viewModel.DetailsPanelBody, StringComparison.Ordinal);
            Assert.Contains("is a FileCrypter encrypted archive.", viewModel.DetailsPanelBody, StringComparison.Ordinal);
            Assert.Contains("Batch page", viewModel.DetailsPanelDetailText, StringComparison.Ordinal);
            Assert.Equal(
                "This file does not require a key file. The password alone unlocks it.",
                viewModel.KeyFileChoiceStatusText);
        });
    }

    [Fact]
    public async Task SourcePath_WhenInspectionFails_StillAllowsDecrypt()
    {
        var workflow = new RecordingWorkflowService
        {
            InspectError = new IOException("The file is locked by another process."),
            Result = new DecryptFileResult("/tmp/plain.txt"),
        };
        var viewModel = new DecryptViewModel(workflow)
        {
            SourcePath = "/tmp/plain.txt.encrypted",
            Password = "secret",
        };

        Assert.False(viewModel.HasInspectedFile);
        Assert.False(viewModel.HasError);
        Assert.Contains("details could not be read", viewModel.DetailsPanelBody, StringComparison.Ordinal);
        Assert.Contains("still enter the password", viewModel.DetailsPanelDetailText, StringComparison.Ordinal);
        Assert.True(viewModel.StartDecryptCommand.CanExecute(null));

        await viewModel.StartDecryptCommand.ExecuteAsync(null);

        Assert.Equal("/tmp/plain.txt", viewModel.ResultPath);
        Assert.False(viewModel.HasError);
    }

    [Fact]
    public void ClearSourceCommand_AfterInspection_RestoresUninspectedPanelText()
    {
        var workflow = new RecordingWorkflowService
        {
            InspectResult = new InspectFileResult(
                FileCrypterPayloadKind.SingleFile,
                IsKeyFileRequired: true,
                IsCompressed: false,
                FormatVersion: 1),
        };
        var viewModel = new DecryptViewModel(workflow)
        {
            SourcePath = "/tmp/plain.txt.encrypted",
        };

        viewModel.ClearSourceCommand.Execute(null);

        Assert.False(viewModel.HasInspectedFile);
        Assert.Equal(
            "Drop an encrypted file to inspect its filename, output target, and key-file requirements.",
            viewModel.DetailsPanelBody);
        Assert.Equal(
            "No key file selected. Decryption will use only the password unless the file requires one.",
            viewModel.KeyFileChoiceStatusText);
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

        public InspectFileResult InspectResult { get; init; } = new(
            FileCrypterPayloadKind.SingleFile,
            IsKeyFileRequired: false,
            IsCompressed: false,
            FormatVersion: 1);

        public Exception? InspectError { get; init; }

        public InspectFileRequest? InspectRequest { get; private set; }

        public int InspectCallCount { get; private set; }

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

        public Task<InspectFileResult> InspectFileAsync(
            InspectFileRequest request,
            CancellationToken cancellationToken)
        {
            InspectRequest = request;
            InspectCallCount++;

            return InspectError is null
                ? Task.FromResult(InspectResult)
                : Task.FromException<InspectFileResult>(InspectError);
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

        public Task<InspectFileResult> InspectFileAsync(
            InspectFileRequest request,
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

        public Task<InspectFileResult> InspectFileAsync(
            InspectFileRequest request,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class DeferredInspectWorkflowService : IFileCrypterWorkflowService
    {
        private readonly Dictionary<string, TaskCompletionSource<InspectFileResult>> pendingInspections =
            new(StringComparer.Ordinal);

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
            throw new NotSupportedException();
        }

        public Task<BatchTransformResult> DecryptFilesAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            throw new NotSupportedException();
        }

        public Task<InspectFileResult> InspectFileAsync(
            InspectFileRequest request,
            CancellationToken cancellationToken)
        {
            TaskCompletionSource<InspectFileResult> completion = new();
            pendingInspections[request.SourcePath] = completion;
            return completion.Task;
        }

        public void Finish(string sourcePath, InspectFileResult result)
        {
            pendingInspections[sourcePath].SetResult(result);
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

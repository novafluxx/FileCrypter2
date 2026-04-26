using FileCrypter.App.Services;
using FileCrypter.Desktop.Tests.TestDoubles;
using FileCrypter.App.ViewModels;
using FileCrypter.Core;
using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.Tests.ViewModels;

public sealed class BatchViewModelTests
{
    [Fact]
    public void StartBatchCommand_RequiresFilesOutputDirectoryAndPassword()
    {
        var viewModel = new BatchViewModel(new RecordingWorkflowService());

        Assert.False(viewModel.StartBatchCommand.CanExecute(null));
        Assert.False(viewModel.ShowBatchReadyAction);
        Assert.False(viewModel.HasError);
        Assert.Contains("Add files", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        viewModel.SourcePaths.Add("/tmp/first.txt");
        Assert.False(viewModel.StartBatchCommand.CanExecute(null));
        Assert.False(viewModel.ShowBatchReadyAction);
        Assert.Contains("output folder", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        using var outputDirectory = new TemporaryDirectory();
        viewModel.OutputDirectory = outputDirectory.Path;
        Assert.False(viewModel.StartBatchCommand.CanExecute(null));
        Assert.False(viewModel.ShowBatchReadyAction);
        Assert.Contains("Enter a password", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);

        viewModel.Password = "secret";
        Assert.True(viewModel.StartBatchCommand.CanExecute(null));
        Assert.True(viewModel.ShowBatchReadyAction);
        Assert.Equal(string.Empty, viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ShowBatchReadyAction_FollowsReadinessAndRunningState()
    {
        using var outputDirectory = new TemporaryDirectory();
        var workflow = new WaitingWorkflowService();
        var viewModel = new BatchViewModel(workflow);

        Assert.False(viewModel.ShowBatchReadyAction);

        viewModel.SourcePaths.Add("/tmp/first.txt");
        Assert.False(viewModel.ShowBatchReadyAction);

        viewModel.OutputDirectory = outputDirectory.Path;
        Assert.False(viewModel.ShowBatchReadyAction);

        viewModel.Password = "secret";
        Assert.True(viewModel.ShowBatchReadyAction);

        Task runTask = viewModel.StartBatchCommand.ExecuteAsync(null);
        await workflow.Started.Task;

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.ShowBatchReadyAction);

        workflow.Finish(new BatchTransformResult(
        [
            new BatchTransformItemResult(
                "/tmp/first.txt",
                Path.Combine(outputDirectory.Path, "first.txt.encrypted"),
                Path.Combine(outputDirectory.Path, "first.txt.encrypted"),
                null),
        ]));
        await runTask;

        Assert.False(viewModel.ShowBatchReadyAction);
    }

    [Fact]
    public void ShowBatchReadyAction_InArchiveDecryptMode_RequiresExactlyOneArchive()
    {
        using var outputDirectory = new TemporaryDirectory();
        var viewModel = new BatchViewModel(new RecordingWorkflowService())
        {
            ArchiveMode = true,
            EncryptMode = false,
            OutputDirectory = outputDirectory.Path,
            Password = "secret",
        };

        Assert.False(viewModel.ShowBatchReadyAction);

        viewModel.SourcePaths.Add("/tmp/first.tar.zst.encrypted");
        viewModel.SourcePaths.Add("/tmp/second.tar.zst.encrypted");
        Assert.False(viewModel.ShowBatchReadyAction);

        viewModel.SourcePaths.RemoveAt(1);
        Assert.True(viewModel.ShowBatchReadyAction);
    }

    [Fact]
    public async Task ChangingInputsAfterFailure_DismissesVisibleErrorAndRestoresValidationMessage()
    {
        using var outputDirectory = new TemporaryDirectory();
        var workflow = new RecordingWorkflowService
        {
            Error = new IOException("The output directory is missing."),
        };
        var viewModel = CreateReadyViewModel(workflow, outputDirectory.Path);

        await viewModel.StartBatchCommand.ExecuteAsync(null);
        Assert.True(viewModel.HasError);
        Assert.Equal("secret", viewModel.Password);

        viewModel.Password = string.Empty;

        Assert.False(viewModel.HasError);
        Assert.Contains("Enter a password", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void StartBatchCommand_InArchiveDecryptMode_RequiresExactlyOneArchive()
    {
        using var outputDirectory = new TemporaryDirectory();
        var viewModel = new BatchViewModel(new RecordingWorkflowService())
        {
            ArchiveMode = true,
            EncryptMode = false,
            OutputDirectory = outputDirectory.Path,
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
        using var sourceDirectory = new TemporaryDirectory();
        string firstPath = Path.Combine(sourceDirectory.Path, "first.txt");
        string secondPath = Path.Combine(sourceDirectory.Path, "second.txt");
        File.WriteAllText(firstPath, "first");
        File.WriteAllText(secondPath, "second");

        var picker = new RecordingFilePickerService
        {
            OpenFilesResult =
            [
                firstPath,
                secondPath,
                firstPath,
            ],
        };
        var viewModel = new BatchViewModel(new RecordingWorkflowService(), picker);

        await viewModel.BrowseFilesCommand.ExecuteAsync(null);

        Assert.Equal(2, viewModel.SourcePaths.Count);
        Assert.Equal(sourceDirectory.Path, viewModel.OutputDirectory);
        Assert.Equal("Choose files to encrypt", picker.LastOpenFilesTitle);
    }

    [Fact]
    public async Task BrowseFilesCommand_InArchiveDecryptMode_UsesSingleFilePickerAndReplacesSelection()
    {
        using var sourceDirectory = new TemporaryDirectory();
        string archivePath = Path.Combine(sourceDirectory.Path, "archive.tar.zst.encrypted");
        File.WriteAllText(archivePath, "archive");

        var picker = new RecordingFilePickerService
        {
            OpenFileResult = archivePath,
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
        Assert.Equal(archivePath, viewModel.SourcePaths[0]);
        Assert.Equal(sourceDirectory.Path, viewModel.OutputDirectory);
        Assert.Equal("Choose an encrypted archive to extract", picker.LastOpenTitle);
    }

    [Fact]
    public async Task BrowseFilesCommand_WithSavedDefaultOutputDirectory_PreservesThatDirectory()
    {
        using var defaultOutputDirectory = new TemporaryDirectory();
        var picker = new RecordingFilePickerService
        {
            OpenFilesResult = ["/tmp/first.txt", "/tmp/second.txt"],
        };
        var viewModel = new BatchViewModel(
            new RecordingWorkflowService(),
            new FileCrypterSettings
            {
                NeverOverwriteExistingFilesByDefault = false,
                DefaultOutputDirectory = defaultOutputDirectory.Path,
            },
            picker);

        await viewModel.BrowseFilesCommand.ExecuteAsync(null);

        Assert.Equal(defaultOutputDirectory.Path, viewModel.OutputDirectory);
        Assert.False(viewModel.NeverOverwriteExistingFiles);
    }

    [Fact]
    public async Task BrowseOutputDirectoryCommand_SetsOutputDirectory()
    {
        using var outputDirectory = new TemporaryDirectory();
        var picker = new RecordingFilePickerService
        {
            FolderResult = outputDirectory.Path,
        };
        var viewModel = new BatchViewModel(new RecordingWorkflowService(), picker);

        await viewModel.BrowseOutputDirectoryCommand.ExecuteAsync(null);

        Assert.Equal(outputDirectory.Path, viewModel.OutputDirectory);
        Assert.Equal("Choose batch output directory", picker.LastFolderTitle);
    }

    [Fact]
    public void ApplyDroppedSourcePaths_AddsFilesSkipsDuplicatesAndAutofillsOutputDirectory()
    {
        using var sourceDirectory = new TemporaryDirectory();
        string firstPath = Path.Combine(sourceDirectory.Path, "first.txt");
        string secondPath = Path.Combine(sourceDirectory.Path, "second.txt");
        File.WriteAllText(firstPath, "first");
        File.WriteAllText(secondPath, "second");

        var viewModel = new BatchViewModel(new RecordingWorkflowService());

        bool applied = viewModel.ApplyDroppedSourcePaths([firstPath, secondPath, firstPath]);

        Assert.True(applied);
        Assert.Equal(2, viewModel.SourcePaths.Count);
        Assert.Equal(firstPath, viewModel.SourcePaths[0]);
        Assert.Equal(secondPath, viewModel.SourcePaths[1]);
        Assert.Equal(sourceDirectory.Path, viewModel.OutputDirectory);
    }

    [Fact]
    public void ApplyDroppedSourcePaths_InArchiveDecryptMode_ReplacesSelection()
    {
        using var sourceDirectory = new TemporaryDirectory();
        string archivePath = Path.Combine(sourceDirectory.Path, "archive.tar.zst.encrypted");
        File.WriteAllText(archivePath, "archive");

        var viewModel = new BatchViewModel(new RecordingWorkflowService())
        {
            ArchiveMode = true,
            EncryptMode = false,
        };
        viewModel.SourcePaths.Add("/tmp/old-first.tar.zst.encrypted");
        viewModel.SourcePaths.Add("/tmp/old-second.tar.zst.encrypted");

        bool applied = viewModel.ApplyDroppedSourcePaths([archivePath]);

        Assert.True(applied);
        Assert.Single(viewModel.SourcePaths);
        Assert.Equal(archivePath, viewModel.SourcePaths[0]);
        Assert.Equal(sourceDirectory.Path, viewModel.OutputDirectory);
    }

    [Fact]
    public void SourceFilePreviews_ShowMetadataAndRemoveTheirOwnFiles()
    {
        using var sourceDirectory = new TemporaryDirectory();
        string firstPath = Path.Combine(sourceDirectory.Path, "first.txt");
        string secondPath = Path.Combine(sourceDirectory.Path, "second.txt");
        File.WriteAllText(firstPath, "first");
        File.WriteAllText(secondPath, "second");

        var viewModel = new BatchViewModel(new RecordingWorkflowService());
        viewModel.SourcePaths.Add(firstPath);
        viewModel.SourcePaths.Add(secondPath);

        Assert.Equal(2, viewModel.SourceFilePreviews.Count);
        Assert.Equal("5 B", viewModel.SourceFilePreviews[0].SizeText);
        Assert.Equal(Path.GetFullPath(firstPath), viewModel.SourceFilePreviews[0].FullPath);

        viewModel.SourceFilePreviews[0].RemoveCommand!.Execute(null);

        Assert.Single(viewModel.SourcePaths);
        Assert.Single(viewModel.SourceFilePreviews);
        Assert.Equal(secondPath, viewModel.SourcePaths[0]);
    }

    [Fact]
    public async Task StartBatchCommand_WhenSuccessful_ReportsSummaryAndResults()
    {
        using var outputDirectory = new TemporaryDirectory();
        var workflow = new RecordingWorkflowService
        {
            BatchResult = new BatchTransformResult(
            [
                new BatchTransformItemResult("/tmp/first.txt", Path.Combine(outputDirectory.Path, "first.txt.encrypted"), Path.Combine(outputDirectory.Path, "first.txt.encrypted"), null),
                new BatchTransformItemResult("/tmp/missing.txt", Path.Combine(outputDirectory.Path, "missing.txt.encrypted"), null, new IOException("Missing input.")),
            ]),
        };
        var viewModel = CreateReadyViewModel(workflow, outputDirectory.Path);

        await viewModel.StartBatchCommand.ExecuteAsync(null);

        Assert.NotNull(workflow.BatchRequest);
        Assert.True(workflow.BatchRequest!.NeverOverwriteExistingFiles);
        Assert.Empty(viewModel.Password);
        Assert.Equal(2, viewModel.Results.Count);
        Assert.Contains("failure", viewModel.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Missing input", viewModel.Results.Single(item => !item.Succeeded).DetailText, StringComparison.Ordinal);
        Assert.Equal("Batch encryption completed with failures", viewModel.StatusText);
        Assert.Equal("1 succeeded, 1 failed - Output folder", viewModel.ProgressText);
        Assert.Equal(outputDirectory.Path, viewModel.FooterActionText);
        Assert.Equal(100, viewModel.ProgressPercent);
    }

    [Fact]
    public async Task StartBatchCommand_InArchiveEncryptMode_PassesArchiveRequestAndReportsOutput()
    {
        using var outputDirectory = new TemporaryDirectory();
        var workflow = new RecordingWorkflowService
        {
            ArchiveEncryptResult = new ArchiveEncryptResult(
                Path.Combine(outputDirectory.Path, "filecrypter-archive-20260418-100000.tar.zst.encrypted")),
        };
        var viewModel = CreateReadyViewModel(workflow, outputDirectory.Path);
        viewModel.ArchiveMode = true;
        viewModel.ArchiveName = "project-docs";

        await viewModel.StartBatchCommand.ExecuteAsync(null);

        Assert.NotNull(workflow.ArchiveEncryptRequest);
        Assert.Equal("project-docs", workflow.ArchiveEncryptRequest!.ArchiveName);
        Assert.Empty(viewModel.Password);
        Assert.Single(viewModel.Results);
        Assert.Contains("bundled", viewModel.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Archive created in ", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal("Saved to", viewModel.ProgressText);
        Assert.Equal(
            Path.Combine(outputDirectory.Path, "filecrypter-archive-20260418-100000.tar.zst.encrypted"),
            viewModel.FooterActionText);
    }

    [Fact]
    public async Task StartBatchCommand_InArchiveDecryptMode_ReportsExtractedResults()
    {
        using var outputDirectory = new TemporaryDirectory();
        var workflow = new RecordingWorkflowService
        {
            ArchiveDecryptResult = new ArchiveDecryptResult(
            [
                Path.Combine(outputDirectory.Path, "first.txt"),
                Path.Combine(outputDirectory.Path, "second.txt"),
            ]),
        };
        var viewModel = new BatchViewModel(workflow)
        {
            ArchiveMode = true,
            EncryptMode = false,
            OutputDirectory = outputDirectory.Path,
            Password = "secret",
            NeverOverwriteExistingFiles = true,
        };
        viewModel.SourcePaths.Add("/tmp/archive.tar.zst.encrypted");

        await viewModel.StartBatchCommand.ExecuteAsync(null);

        Assert.NotNull(workflow.ArchiveDecryptRequest);
        Assert.Equal("/tmp/archive.tar.zst.encrypted", workflow.ArchiveDecryptRequest!.SourcePath);
        Assert.Empty(viewModel.Password);
        Assert.Equal(2, viewModel.Results.Count);
        Assert.All(viewModel.Results, item => Assert.True(item.Succeeded));
        Assert.Contains("extracted", viewModel.ResultSummary, StringComparison.OrdinalIgnoreCase);
        Assert.StartsWith("Archive extracted in ", viewModel.StatusText, StringComparison.Ordinal);
        Assert.Equal($"Extracted 2 file(s) to", viewModel.ProgressText);
        Assert.Equal(outputDirectory.Path, viewModel.FooterActionText);
    }

    [Fact]
    public async Task CopyResultsCommand_CopiesSummaryAndItemDetails()
    {
        using var outputDirectory = new TemporaryDirectory();
        var clipboard = new RecordingClipboardService();
        var workflow = new RecordingWorkflowService
        {
            BatchResult = new BatchTransformResult(
            [
                new BatchTransformItemResult(
                    "/tmp/first.txt",
                    Path.Combine(outputDirectory.Path, "first.txt.encrypted"),
                    Path.Combine(outputDirectory.Path, "first.txt.encrypted"),
                    null),
                new BatchTransformItemResult(
                    "/tmp/missing.txt",
                    Path.Combine(outputDirectory.Path, "missing.txt.encrypted"),
                    null,
                    new IOException("Missing input.")),
            ]),
        };
        var viewModel = new BatchViewModel(workflow, filePickerService: null, clipboardService: clipboard)
        {
            OutputDirectory = outputDirectory.Path,
            Password = "secret",
        };
        viewModel.SourcePaths.Add("/tmp/first.txt");
        viewModel.SourcePaths.Add("/tmp/missing.txt");
        WorkflowToastNotification? toast = null;
        viewModel.ToastNotificationRequested += (_, notification) => toast = notification;

        await viewModel.StartBatchCommand.ExecuteAsync(null);
        string progressTextAfterRun = viewModel.ProgressText;
        await viewModel.CopyResultsCommand.ExecuteAsync(null);

        Assert.NotNull(clipboard.LastText);
        Assert.Contains("Batch encryption finished with 1 failure", clipboard.LastText, StringComparison.Ordinal);
        Assert.Contains("Succeeded: first.txt", clipboard.LastText, StringComparison.Ordinal);
        Assert.Contains("OUTPUT: ", clipboard.LastText, StringComparison.Ordinal);
        Assert.Contains("Failed: missing.txt", clipboard.LastText, StringComparison.Ordinal);
        Assert.Contains("ISSUE: Path error: Missing input.", clipboard.LastText, StringComparison.Ordinal);
        Assert.Equal(progressTextAfterRun, viewModel.ProgressText);
        Assert.NotNull(toast);
        Assert.Equal("Copied", toast.Title);
        Assert.Equal("Copied batch results.", toast.Message);
    }

    [Fact]
    public async Task StartBatchCommand_WhileRunning_DisablesCommands()
    {
        using var outputDirectory = new TemporaryDirectory();
        var workflow = new WaitingWorkflowService();
        var viewModel = CreateReadyViewModel(workflow, outputDirectory.Path);

        Task runTask = viewModel.StartBatchCommand.ExecuteAsync(null);
        await workflow.Started.Task;

        Assert.True(viewModel.IsRunning);
        Assert.False(viewModel.StartBatchCommand.CanExecute(null));
        Assert.False(viewModel.ClearFilesCommand.CanExecute(null));

        workflow.Finish(new BatchTransformResult(
        [
            new BatchTransformItemResult(
                "/tmp/first.txt",
                Path.Combine(outputDirectory.Path, "first.txt.encrypted"),
                Path.Combine(outputDirectory.Path, "first.txt.encrypted"),
                null),
        ]));
        await runTask;

        Assert.False(viewModel.IsRunning);
        Assert.False(viewModel.StartBatchCommand.CanExecute(null));
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

    [Fact]
    public void WorkflowKindDescription_FollowsSelectedCombination()
    {
        var viewModel = new BatchViewModel(new RecordingWorkflowService());

        Assert.Contains("individual-file encryption", viewModel.WorkflowKindDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("own encrypted output", viewModel.WorkflowKindDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Encrypt each selected file", viewModel.EncryptActionDescription, StringComparison.OrdinalIgnoreCase);

        viewModel.IsDecryptMode = true;

        Assert.Contains("individual-file decryption", viewModel.WorkflowKindDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Decrypt each selected file separately", viewModel.DecryptActionDescription, StringComparison.OrdinalIgnoreCase);

        viewModel.ArchiveMode = true;

        Assert.Contains("archive extraction", viewModel.WorkflowKindDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("exactly one encrypted archive", viewModel.WorkflowKindDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("extract one encrypted archive", viewModel.DecryptActionDescription, StringComparison.OrdinalIgnoreCase);

        viewModel.EncryptMode = true;

        Assert.Contains("archive encryption", viewModel.WorkflowKindDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(".tar.zst.encrypted archive", viewModel.WorkflowKindDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("create one encrypted archive", viewModel.EncryptActionDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TogglePasswordVisibilityCommand_TogglesVisiblePasswordState()
    {
        var viewModel = new BatchViewModel(new RecordingWorkflowService());

        Assert.True(viewModel.ShowMaskedPasswordInput);
        Assert.Equal("Show", viewModel.PasswordVisibilityActionText);

        viewModel.TogglePasswordVisibilityCommand.Execute(null);

        Assert.False(viewModel.ShowMaskedPasswordInput);
        Assert.True(viewModel.ShowPassword);
        Assert.Equal("Hide", viewModel.PasswordVisibilityActionText);
    }

    [Fact]
    public void GenerateRandomPasswordCommand_FillsPasswordShowsItAndUpdatesStrength()
    {
        var generator = new RecordingPasswordGeneratorService
        {
            RandomPassword = "Generated!Password123456",
        };
        var viewModel = new BatchViewModel(
            new RecordingWorkflowService(),
            passwordGeneratorService: generator);

        viewModel.GenerateRandomPasswordCommand.Execute(null);

        Assert.Equal("Generated!Password123456", viewModel.Password);
        Assert.True(viewModel.ShowPassword);
        Assert.False(viewModel.ShowMaskedPasswordInput);
        Assert.Equal(4, viewModel.PasswordStrengthScore);
        Assert.Equal(100d, viewModel.PasswordStrengthPercent);
        Assert.Equal("Excellent", viewModel.PasswordStrengthLabel);
    }

    [Fact]
    public void GenerateMemorablePassphraseCommand_FillsPasswordShowsItAndUpdatesStrength()
    {
        var generator = new RecordingPasswordGeneratorService
        {
            MemorablePassphrase = "river-lantern-copper-signal-violet",
        };
        var viewModel = new BatchViewModel(
            new RecordingWorkflowService(),
            passwordGeneratorService: generator);

        viewModel.GenerateMemorablePassphraseCommand.Execute(null);

        Assert.Equal("river-lantern-copper-signal-violet", viewModel.Password);
        Assert.True(viewModel.ShowPassword);
        Assert.False(viewModel.ShowMaskedPasswordInput);
        Assert.Equal(4, viewModel.PasswordStrengthScore);
        Assert.Equal("Excellent", viewModel.PasswordStrengthLabel);
    }

    [Fact]
    public void PasswordStrengthProperties_UpdateWhenPasswordChanges()
    {
        var viewModel = new BatchViewModel(new RecordingWorkflowService())
        {
            Password = "short",
        };

        Assert.Equal(0, viewModel.PasswordStrengthScore);
        Assert.Equal("Enter a password", viewModel.PasswordStrengthLabel);

        viewModel.Password = "Strong!Pass123";

        Assert.Equal(4, viewModel.PasswordStrengthScore);
        Assert.Equal(100d, viewModel.PasswordStrengthPercent);
        Assert.Equal("Excellent", viewModel.PasswordStrengthLabel);
        Assert.Contains("bits of estimated entropy", viewModel.PasswordStrengthDetail, StringComparison.Ordinal);
    }

    [Fact]
    public async Task QueueInspectorBody_ReflectsResultsWhenPresent()
    {
        using var outputDirectory = new TemporaryDirectory();
        var workflow = new RecordingWorkflowService
        {
            BatchResult = new BatchTransformResult(
            [
                new BatchTransformItemResult(
                    "/tmp/first.txt",
                    Path.Combine(outputDirectory.Path, "first.txt.encrypted"),
                    Path.Combine(outputDirectory.Path, "first.txt.encrypted"),
                    null),
            ]),
        };
        var viewModel = CreateReadyViewModel(workflow, outputDirectory.Path);

        await viewModel.StartBatchCommand.ExecuteAsync(null);

        Assert.Contains("complete", viewModel.QueueInspectorBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void WorkflowChooserDescription_ExplainsThatAllFourCombinationsAreAvailable()
    {
        var viewModel = new BatchViewModel(new RecordingWorkflowService());

        Assert.Contains("all four combinations", viewModel.WorkflowChooserDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("archive extraction", viewModel.WorkflowChooserDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one output per file", viewModel.IndividualFilesOptionDescription, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("one encrypted archive", viewModel.ArchiveModeOptionDescription, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void InvalidArchiveName_BlocksStartAndExplainsIssue()
    {
        using var outputDirectory = new TemporaryDirectory();
        var viewModel = new BatchViewModel(new RecordingWorkflowService())
        {
            ArchiveMode = true,
            OutputDirectory = outputDirectory.Path,
            Password = "secret",
            ArchiveName = "bad/name",
        };
        viewModel.SourcePaths.Add("/tmp/first.txt");

        Assert.False(viewModel.StartBatchCommand.CanExecute(null));
        Assert.Contains("unsafe", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ChangingSelectedFilesAfterRun_ClearsStaleResults()
    {
        using var outputDirectory = new TemporaryDirectory();
        var workflow = new RecordingWorkflowService
        {
            BatchResult = new BatchTransformResult(
            [
                new BatchTransformItemResult(
                    "/tmp/first.txt",
                    Path.Combine(outputDirectory.Path, "first.txt.encrypted"),
                    Path.Combine(outputDirectory.Path, "first.txt.encrypted"),
                    null),
            ]),
        };
        var viewModel = CreateReadyViewModel(workflow, outputDirectory.Path);

        await viewModel.StartBatchCommand.ExecuteAsync(null);
        Assert.True(viewModel.HasResults);

        viewModel.SourcePaths.Add("/tmp/third.txt");

        Assert.False(viewModel.HasResults);
        Assert.False(viewModel.HasResultSummary);
        Assert.Contains("Enter a password", viewModel.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("3 files selected - first.txt + 2 more", viewModel.ProgressText);
    }

    private static BatchViewModel CreateReadyViewModel(
        IFileCrypterWorkflowService workflowService,
        string outputDirectory)
    {
        var viewModel = new BatchViewModel(workflowService)
        {
            OutputDirectory = outputDirectory,
            Password = "secret",
            NeverOverwriteExistingFiles = true,
        };
        viewModel.SourcePaths.Add("/tmp/first.txt");
        viewModel.SourcePaths.Add("/tmp/second.txt");
        return viewModel;
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

    private sealed class RecordingPasswordGeneratorService : IPasswordGeneratorService
    {
        public string RandomPassword { get; init; } = "Random!Password123456789";

        public string MemorablePassphrase { get; init; } = "river-lantern-copper-signal-violet";

        public string GenerateRandomPassword()
        {
            return RandomPassword;
        }

        public string GenerateMemorablePassphrase()
        {
            return MemorablePassphrase;
        }
    }
}

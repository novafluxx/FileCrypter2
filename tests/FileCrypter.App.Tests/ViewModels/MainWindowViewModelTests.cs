using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_DefaultsToEncryptPage()
    {
        var viewModel = new MainWindowViewModel(
            new StubWorkflowService(),
            new StubSettingsService(),
            null,
            new StubAppMetadataService());

        Assert.IsType<EncryptViewModel>(viewModel.CurrentPage);
        Assert.Equal("Encrypt a file", viewModel.CurrentPageTitle);
        Assert.Equal("Encrypt", viewModel.PrimaryNavigationItems.Single(item => item.IsSelected).Title);
    }

    [Fact]
    public void Constructor_AppliesPersistedCompressionDefaultToEncryptPage()
    {
        using var outputDirectory = new TemporaryDirectory();
        var viewModel = new MainWindowViewModel(
            new StubWorkflowService(),
            new StubSettingsService
            {
                LoadedSettings = new FileCrypterSettings
                {
                    EnableCompressionByDefault = true,
                    NeverOverwriteExistingFilesByDefault = false,
                    DefaultOutputDirectory = outputDirectory.Path,
                },
            },
            null,
            new StubAppMetadataService());

        EncryptViewModel encryptPage = Assert.IsType<EncryptViewModel>(viewModel.CurrentPage);
        Assert.True(encryptPage.EnableCompression);
        Assert.False(encryptPage.NeverOverwriteExistingFiles);
        encryptPage.SourcePath = "/tmp/plain.txt";
        Assert.Equal(Path.Combine(outputDirectory.Path, "plain.txt.encrypted"), encryptPage.OutputPath);
    }

    [Fact]
    public void SelectNavigationItem_HelpPageUsesHelpViewModelAndFooter()
    {
        var viewModel = new MainWindowViewModel(
            new StubWorkflowService(),
            new StubSettingsService(),
            null,
            new StubAppMetadataService(),
            new StubAppUpdateService());
        NavigationItemViewModel helpItem = viewModel.SecondaryNavigationItems.Single(item => item.Key == "help");

        viewModel.SelectNavigationItemCommand.Execute(helpItem);

        Assert.IsType<HelpViewModel>(viewModel.CurrentPage);
        Assert.Equal("Help and recovery", viewModel.CurrentPageTitle);
        Assert.Equal("Help and recovery", viewModel.StatusText);
        Assert.Equal("Update checks are not configured for this development build yet.", viewModel.FooterDetail);
        Assert.True(helpItem.IsSelected);
        Assert.All(
            viewModel.PrimaryNavigationItems.Concat(viewModel.SecondaryNavigationItems).Where(item => item != helpItem),
            item => Assert.False(item.IsSelected));
    }

    [Fact]
    public void SelectNavigationItem_DecryptPageUsesDecryptViewModelAndFooter()
    {
        var viewModel = new MainWindowViewModel(
            new StubWorkflowService(),
            new StubSettingsService(),
            null,
            new StubAppMetadataService());
        NavigationItemViewModel decryptItem = viewModel.PrimaryNavigationItems.Single(item => item.Key == "decrypt");

        viewModel.SelectNavigationItemCommand.Execute(decryptItem);

        Assert.IsType<DecryptViewModel>(viewModel.CurrentPage);
        Assert.Equal("Decrypt a file", viewModel.CurrentPageTitle);
        Assert.Equal("Ready", viewModel.StatusText);
        Assert.Equal("No encrypted file selected", viewModel.FooterDetail);
    }

    [Fact]
    public void SelectNavigationItem_BatchPageUsesBatchViewModelAndFooter()
    {
        var viewModel = new MainWindowViewModel(
            new StubWorkflowService(),
            new StubSettingsService(),
            null,
            new StubAppMetadataService());
        NavigationItemViewModel batchItem = viewModel.PrimaryNavigationItems.Single(item => item.Key == "batch");

        viewModel.SelectNavigationItemCommand.Execute(batchItem);

        Assert.IsType<BatchViewModel>(viewModel.CurrentPage);
        Assert.Equal("Batch workflows", viewModel.CurrentPageTitle);
        Assert.Equal("Ready", viewModel.StatusText);
        Assert.Equal("No batch files selected", viewModel.FooterDetail);
    }

    [Fact]
    public async Task SettingsSave_UpdatesEncryptCompressionDefault()
    {
        using var outputDirectory = new TemporaryDirectory();
        var settingsService = new StubSettingsService();
        var viewModel = new MainWindowViewModel(
            new StubWorkflowService(),
            settingsService,
            null,
            new StubAppMetadataService());
        NavigationItemViewModel settingsItem = viewModel.SecondaryNavigationItems.Single(item => item.Key == "settings");
        NavigationItemViewModel encryptItem = viewModel.PrimaryNavigationItems.Single(item => item.Key == "encrypt");

        viewModel.SelectNavigationItemCommand.Execute(settingsItem);
        SettingsViewModel settingsPage = Assert.IsType<SettingsViewModel>(viewModel.CurrentPage);
        settingsPage.EnableCompressionByDefault = true;
        settingsPage.NeverOverwriteExistingFilesByDefault = false;
        settingsPage.DefaultOutputDirectory = outputDirectory.Path;

        await settingsPage.SaveSettingsCommand.ExecuteAsync(null);
        viewModel.SelectNavigationItemCommand.Execute(encryptItem);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.True(settingsService.SavedSettings.EnableCompressionByDefault);
        Assert.False(settingsService.SavedSettings.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(outputDirectory.Path, settingsService.SavedSettings.DefaultOutputDirectory);
        EncryptViewModel encryptPage = Assert.IsType<EncryptViewModel>(viewModel.CurrentPage);
        Assert.True(encryptPage.EnableCompression);
        Assert.False(encryptPage.NeverOverwriteExistingFiles);
        encryptPage.SourcePath = "/tmp/plain.txt";
        Assert.Equal(Path.Combine(outputDirectory.Path, "plain.txt.encrypted"), encryptPage.OutputPath);
    }

    [Fact]
    public void Constructor_UsesMetadataServiceForVersion()
    {
        var viewModel = new MainWindowViewModel(
            new StubWorkflowService(),
            new StubSettingsService(),
            null,
            new StubAppMetadataService("v5.4.3"));

        Assert.Equal("v5.4.3", viewModel.AppVersion);
    }

    private sealed class StubWorkflowService : IFileCrypterWorkflowService
    {
        public Task<EncryptFileResult> EncryptFileAsync(
            EncryptFileRequest request,
            IProgress<FileCrypter.Core.FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new EncryptFileResult("unused", null));
        }

        public Task<DecryptFileResult> DecryptFileAsync(
            DecryptFileRequest request,
            IProgress<FileCrypter.Core.FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new DecryptFileResult("unused"));
        }

        public Task<ArchiveEncryptResult> EncryptArchiveAsync(
            ArchiveEncryptRequest request,
            IProgress<FileCrypter.Core.FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ArchiveEncryptResult("unused"));
        }

        public Task<ArchiveDecryptResult> DecryptArchiveAsync(
            ArchiveDecryptRequest request,
            IProgress<FileCrypter.Core.FileCrypterProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new ArchiveDecryptResult([]));
        }

        public Task<BatchTransformResult> EncryptFilesAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new BatchTransformResult([]));
        }

        public Task<BatchTransformResult> DecryptFilesAsync(
            BatchTransformRequest request,
            IProgress<BatchOperationProgress>? progress,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(new BatchTransformResult([]));
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

    private sealed class StubSettingsService : IFileCrypterSettingsService
    {
        public string SettingsPath => "/tmp/settings.json";

        public FileCrypterSettings LoadedSettings { get; set; } = new();

        public FileCrypterSettings? SavedSettings { get; private set; }

        public Exception? LoadException { get; init; }

        public Exception? SaveException { get; init; }

        public Task<FileCrypterSettings> LoadAsync(CancellationToken cancellationToken)
        {
            return LoadException is null
                ? Task.FromResult(LoadedSettings)
                : Task.FromException<FileCrypterSettings>(LoadException);
        }

        public Task SaveAsync(FileCrypterSettings settings, CancellationToken cancellationToken)
        {
            if (SaveException is not null)
            {
                return Task.FromException(SaveException);
            }

            SavedSettings = settings;
            LoadedSettings = settings;
            return Task.CompletedTask;
        }
    }

    private sealed class StubAppMetadataService(
        string displayVersion = "v0.1.0",
        ushort formatVersion = 1) : IAppMetadataService
    {
        public string DisplayVersion { get; } = displayVersion;

        public ushort FormatVersion { get; } = formatVersion;
    }

    private sealed class StubAppUpdateService : IAppUpdateService
    {
        public AppUpdateCheckResult GetCurrentStatus()
        {
            return new AppUpdateCheckResult(
                AppUpdateStatus.NotConfigured,
                "Update checks are not configured for this development build yet.",
                "This test build does not have a release channel.");
        }

        public Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(GetCurrentStatus());
        }
    }
}

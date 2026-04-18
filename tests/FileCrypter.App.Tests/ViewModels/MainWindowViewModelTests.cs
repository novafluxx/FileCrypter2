using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_DefaultsToEncryptPage()
    {
        var viewModel = new MainWindowViewModel(new StubWorkflowService(), new StubSettingsService());

        Assert.IsType<EncryptViewModel>(viewModel.CurrentPage);
        Assert.Equal("Encrypt a file", viewModel.CurrentPageTitle);
        Assert.Equal("Encrypt", viewModel.PrimaryNavigationItems.Single(item => item.IsSelected).Title);
    }

    [Fact]
    public void Constructor_AppliesPersistedCompressionDefaultToEncryptPage()
    {
        var viewModel = new MainWindowViewModel(
            new StubWorkflowService(),
            new StubSettingsService
            {
                LoadedSettings = new FileCrypterSettings { EnableCompressionByDefault = true },
            });

        EncryptViewModel encryptPage = Assert.IsType<EncryptViewModel>(viewModel.CurrentPage);
        Assert.True(encryptPage.EnableCompression);
    }

    [Fact]
    public void SelectNavigationItem_SwapsCurrentPage()
    {
        var viewModel = new MainWindowViewModel(new StubWorkflowService(), new StubSettingsService());
        NavigationItemViewModel helpItem = viewModel.SecondaryNavigationItems.Single(item => item.Key == "help");

        viewModel.SelectNavigationItemCommand.Execute(helpItem);

        Assert.IsType<PlaceholderPageViewModel>(viewModel.CurrentPage);
        Assert.Equal("Help", viewModel.CurrentPageTitle);
        Assert.True(helpItem.IsSelected);
        Assert.All(
            viewModel.PrimaryNavigationItems.Concat(viewModel.SecondaryNavigationItems).Where(item => item != helpItem),
            item => Assert.False(item.IsSelected));
    }

    [Fact]
    public void SelectNavigationItem_DecryptPageUsesDecryptViewModelAndFooter()
    {
        var viewModel = new MainWindowViewModel(new StubWorkflowService(), new StubSettingsService());
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
        var viewModel = new MainWindowViewModel(new StubWorkflowService(), new StubSettingsService());
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
        var settingsService = new StubSettingsService();
        var viewModel = new MainWindowViewModel(new StubWorkflowService(), settingsService);
        NavigationItemViewModel settingsItem = viewModel.SecondaryNavigationItems.Single(item => item.Key == "settings");
        NavigationItemViewModel encryptItem = viewModel.PrimaryNavigationItems.Single(item => item.Key == "encrypt");

        viewModel.SelectNavigationItemCommand.Execute(settingsItem);
        SettingsViewModel settingsPage = Assert.IsType<SettingsViewModel>(viewModel.CurrentPage);
        settingsPage.EnableCompressionByDefault = true;

        await settingsPage.SaveSettingsCommand.ExecuteAsync(null);
        viewModel.SelectNavigationItemCommand.Execute(encryptItem);

        Assert.NotNull(settingsService.SavedSettings);
        Assert.True(settingsService.SavedSettings.EnableCompressionByDefault);
        Assert.True(Assert.IsType<EncryptViewModel>(viewModel.CurrentPage).EnableCompression);
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
}

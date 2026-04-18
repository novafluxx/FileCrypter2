using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;

namespace FileCrypter.App.Tests.ViewModels;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_DefaultsToEncryptPage()
    {
        var viewModel = new MainWindowViewModel(new StubWorkflowService());

        Assert.IsType<EncryptViewModel>(viewModel.CurrentPage);
        Assert.Equal("Encrypt a file", viewModel.CurrentPageTitle);
        Assert.Equal("Encrypt", viewModel.PrimaryNavigationItems.Single(item => item.IsSelected).Title);
    }

    [Fact]
    public void SelectNavigationItem_SwapsCurrentPage()
    {
        var viewModel = new MainWindowViewModel(new StubWorkflowService());
        NavigationItemViewModel helpItem = viewModel.SecondaryNavigationItems.Single(item => item.Key == "help");

        viewModel.SelectNavigationItemCommand.Execute(helpItem);

        Assert.IsType<PlaceholderPageViewModel>(viewModel.CurrentPage);
        Assert.Equal("Help", viewModel.CurrentPageTitle);
        Assert.True(helpItem.IsSelected);
        Assert.All(
            viewModel.PrimaryNavigationItems.Concat(viewModel.SecondaryNavigationItems).Where(item => item != helpItem),
            item => Assert.False(item.IsSelected));
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
    }
}

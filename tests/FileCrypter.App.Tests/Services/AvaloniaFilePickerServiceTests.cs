using Avalonia.Platform.Storage;
using FileCrypter.App.Services;

namespace FileCrypter.App.Tests.Services;

public sealed class AvaloniaFilePickerServiceTests
{
    [Fact]
    public async Task PickOpenFileAsync_ForwardsTitleAndReturnsSelectedPath()
    {
        FilePickerOpenOptions? capturedOptions = null;
        var service = new AvaloniaFilePickerService(
            options =>
            {
                capturedOptions = options;
                return Task.FromResult<string?>("/tmp/plain.txt");
            },
            _ => Task.FromResult<string?>(null));

        string? result = await service.PickOpenFileAsync("Choose a file to encrypt", CancellationToken.None);

        Assert.Equal("/tmp/plain.txt", result);
        Assert.NotNull(capturedOptions);
        Assert.Equal("Choose a file to encrypt", capturedOptions!.Title);
        Assert.False(capturedOptions.AllowMultiple);
    }

    [Fact]
    public async Task PickOpenFilesAsync_ForwardsMultiSelectAndReturnsSelectedPaths()
    {
        FilePickerOpenOptions? capturedOptions = null;
        var service = new AvaloniaFilePickerService(
            options =>
            {
                capturedOptions = options;
                return Task.FromResult<IReadOnlyList<string>>(
                [
                    "/tmp/first.txt",
                    "/tmp/second.txt",
                ]);
            },
            _ => Task.FromResult<string?>(null),
            _ => Task.FromResult<string?>(null));

        IReadOnlyList<string> result = await service.PickOpenFilesAsync("Choose files to encrypt", CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("/tmp/first.txt", result[0]);
        Assert.Equal("/tmp/second.txt", result[1]);
        Assert.NotNull(capturedOptions);
        Assert.Equal("Choose files to encrypt", capturedOptions!.Title);
        Assert.True(capturedOptions.AllowMultiple);
    }

    [Fact]
    public async Task PickOpenFolderAsync_ForwardsTitleAndReturnsSelectedPath()
    {
        FolderPickerOpenOptions? capturedOptions = null;
        var service = new AvaloniaFilePickerService(
            _ => Task.FromResult<IReadOnlyList<string>>([]),
            _ => Task.FromResult<string?>(null),
            options =>
            {
                capturedOptions = options;
                return Task.FromResult<string?>("/tmp/output");
            });

        string? result = await service.PickOpenFolderAsync("Choose output directory", CancellationToken.None);

        Assert.Equal("/tmp/output", result);
        Assert.NotNull(capturedOptions);
        Assert.Equal("Choose output directory", capturedOptions!.Title);
        Assert.False(capturedOptions.AllowMultiple);
    }

    [Fact]
    public async Task PickSaveFileAsync_ForwardsSuggestedFileNameAndReturnsSelectedPath()
    {
        FilePickerSaveOptions? capturedOptions = null;
        var service = new AvaloniaFilePickerService(
            _ => Task.FromResult<string?>(null),
            options =>
            {
                capturedOptions = options;
                return Task.FromResult<string?>("/tmp/plain.txt.encrypted");
            });

        string? result = await service.PickSaveFileAsync(
            "Choose encrypted output file",
            "plain.txt.encrypted",
            CancellationToken.None);

        Assert.Equal("/tmp/plain.txt.encrypted", result);
        Assert.NotNull(capturedOptions);
        Assert.Equal("Choose encrypted output file", capturedOptions!.Title);
        Assert.Equal("plain.txt.encrypted", capturedOptions.SuggestedFileName);
    }

    [Fact]
    public async Task PickOpenFileAsync_WhenNoFileIsSelected_ReturnsNull()
    {
        var service = new AvaloniaFilePickerService(
            _ => Task.FromResult<string?>(null),
            _ => Task.FromResult<string?>(null));

        string? result = await service.PickOpenFileAsync("Choose a file to encrypt", CancellationToken.None);

        Assert.Null(result);
    }
}

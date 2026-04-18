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

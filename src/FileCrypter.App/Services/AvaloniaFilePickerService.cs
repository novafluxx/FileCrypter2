using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace FileCrypter.App.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    private readonly Window owner;

    public AvaloniaFilePickerService(Window owner)
    {
        this.owner = owner;
    }

    public async Task<string?> PickOpenFileAsync(string title, CancellationToken cancellationToken)
    {
        IReadOnlyList<IStorageFile> files = await owner.StorageProvider.OpenFilePickerAsync(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            }).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    public async Task<string?> PickSaveFileAsync(string title, string? suggestedFileName, CancellationToken cancellationToken)
    {
        IStorageFile? file = await owner.StorageProvider.SaveFilePickerAsync(
            new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedFileName,
            }).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        return file?.TryGetLocalPath();
    }
}

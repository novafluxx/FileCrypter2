using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace FileCrypter.App.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    private readonly Func<FilePickerOpenOptions, Task<string?>> openFilePicker;
    private readonly Func<FilePickerSaveOptions, Task<string?>> saveFilePicker;

    public AvaloniaFilePickerService(Window owner)
        : this(
            options => PickOpenFileAsync(owner.StorageProvider, options),
            options => PickSaveFileAsync(owner.StorageProvider, options))
    {
    }

    public AvaloniaFilePickerService(
        Func<FilePickerOpenOptions, Task<string?>> openFilePicker,
        Func<FilePickerSaveOptions, Task<string?>> saveFilePicker)
    {
        this.openFilePicker = openFilePicker;
        this.saveFilePicker = saveFilePicker;
    }

    public async Task<string?> PickOpenFileAsync(string title, CancellationToken cancellationToken)
    {
        string? selectedPath = await openFilePicker(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            }).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        return selectedPath;
    }

    public async Task<string?> PickSaveFileAsync(string title, string? suggestedFileName, CancellationToken cancellationToken)
    {
        string? selectedPath = await saveFilePicker(
            new FilePickerSaveOptions
            {
                Title = title,
                SuggestedFileName = suggestedFileName,
            }).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        return selectedPath;
    }

    private static async Task<string?> PickOpenFileAsync(
        IStorageProvider storageProvider,
        FilePickerOpenOptions options)
    {
        IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(options).ConfigureAwait(true);
        return files.Count == 0 ? null : files[0].TryGetLocalPath();
    }

    private static async Task<string?> PickSaveFileAsync(
        IStorageProvider storageProvider,
        FilePickerSaveOptions options)
    {
        IStorageFile? file = await storageProvider.SaveFilePickerAsync(options).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }
}

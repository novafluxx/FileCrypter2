using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace FileCrypter.Desktop.Services;

public sealed class AvaloniaFilePickerService : IFilePickerService
{
    private readonly Func<FilePickerOpenOptions, Task<IReadOnlyList<string>>> openFilePicker;
    private readonly Func<FilePickerSaveOptions, Task<string?>> saveFilePicker;
    private readonly Func<FolderPickerOpenOptions, Task<string?>> openFolderPicker;

    public AvaloniaFilePickerService(Window owner)
        : this(
            options => PickOpenFilesAsync(owner.StorageProvider, options),
            options => PickSaveFileAsync(owner.StorageProvider, options),
            options => PickOpenFolderAsync(owner.StorageProvider, options))
    {
    }

    public AvaloniaFilePickerService(
        Func<FilePickerOpenOptions, Task<string?>> openFilePicker,
        Func<FilePickerSaveOptions, Task<string?>> saveFilePicker)
        : this(
            async options =>
            {
                string? selectedPath = await openFilePicker(options).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(selectedPath) ? [] : [selectedPath];
            },
            saveFilePicker,
            _ => Task.FromResult<string?>(null))
    {
    }

    public AvaloniaFilePickerService(
        Func<FilePickerOpenOptions, Task<IReadOnlyList<string>>> openFilePicker,
        Func<FilePickerSaveOptions, Task<string?>> saveFilePicker,
        Func<FolderPickerOpenOptions, Task<string?>> openFolderPicker)
    {
        this.openFilePicker = openFilePicker;
        this.saveFilePicker = saveFilePicker;
        this.openFolderPicker = openFolderPicker;
    }

    public async Task<string?> PickOpenFileAsync(string title, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> selectedPaths = await openFilePicker(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = false,
            }).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        return selectedPaths.Count == 0 ? null : selectedPaths[0];
    }

    public async Task<IReadOnlyList<string>> PickOpenFilesAsync(string title, CancellationToken cancellationToken)
    {
        IReadOnlyList<string> selectedPaths = await openFilePicker(
            new FilePickerOpenOptions
            {
                Title = title,
                AllowMultiple = true,
            }).ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();
        return selectedPaths;
    }

    public async Task<string?> PickOpenFolderAsync(string title, CancellationToken cancellationToken)
    {
        string? selectedPath = await openFolderPicker(
            new FolderPickerOpenOptions
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

    private static async Task<IReadOnlyList<string>> PickOpenFilesAsync(
        IStorageProvider storageProvider,
        FilePickerOpenOptions options)
    {
        IReadOnlyList<IStorageFile> files = await storageProvider.OpenFilePickerAsync(options).ConfigureAwait(true);
        return files
            .Select(file => file.TryGetLocalPath())
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Cast<string>()
            .ToArray();
    }

    private static async Task<string?> PickOpenFolderAsync(
        IStorageProvider storageProvider,
        FolderPickerOpenOptions options)
    {
        IReadOnlyList<IStorageFolder> folders = await storageProvider.OpenFolderPickerAsync(options).ConfigureAwait(true);
        return folders.Count == 0 ? null : folders[0].TryGetLocalPath();
    }

    private static async Task<string?> PickSaveFileAsync(
        IStorageProvider storageProvider,
        FilePickerSaveOptions options)
    {
        IStorageFile? file = await storageProvider.SaveFilePickerAsync(options).ConfigureAwait(true);
        return file?.TryGetLocalPath();
    }
}

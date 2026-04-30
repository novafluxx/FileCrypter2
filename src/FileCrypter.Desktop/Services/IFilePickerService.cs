namespace FileCrypter.Desktop.Services;

public interface IFilePickerService
{
    Task<string?> PickOpenFileAsync(string title, CancellationToken cancellationToken);

    Task<IReadOnlyList<string>> PickOpenFilesAsync(string title, CancellationToken cancellationToken);

    Task<string?> PickOpenFolderAsync(string title, CancellationToken cancellationToken);

    Task<string?> PickSaveFileAsync(string title, string? suggestedFileName, CancellationToken cancellationToken);
}

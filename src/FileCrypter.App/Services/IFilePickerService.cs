namespace FileCrypter.App.Services;

public interface IFilePickerService
{
    Task<string?> PickOpenFileAsync(string title, CancellationToken cancellationToken);

    Task<string?> PickSaveFileAsync(string title, string? suggestedFileName, CancellationToken cancellationToken);
}

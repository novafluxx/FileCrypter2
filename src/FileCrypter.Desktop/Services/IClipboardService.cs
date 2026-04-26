namespace FileCrypter.App.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken);
}

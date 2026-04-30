namespace FileCrypter.Desktop.Services;

public interface IClipboardService
{
    Task SetTextAsync(string text, CancellationToken cancellationToken);
}

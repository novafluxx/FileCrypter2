using Avalonia.Controls;
using Avalonia.Input.Platform;

namespace FileCrypter.App.Services;

public sealed class AvaloniaClipboardService : IClipboardService
{
    private readonly TopLevel owner;

    public AvaloniaClipboardService(TopLevel owner)
    {
        this.owner = owner ?? throw new ArgumentNullException(nameof(owner));
    }

    public async Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        cancellationToken.ThrowIfCancellationRequested();

        IClipboard? clipboard = owner.Clipboard;
        if (clipboard is null)
        {
            throw new InvalidOperationException("Clipboard is unavailable for this window.");
        }

        await clipboard.SetTextAsync(text).ConfigureAwait(true);
        cancellationToken.ThrowIfCancellationRequested();
    }
}

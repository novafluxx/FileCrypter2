using FileCrypter.App.Services;

namespace FileCrypter.Desktop.Tests.TestDoubles;

internal sealed class RecordingClipboardService : IClipboardService
{
    public string? LastText { get; private set; }

    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastText = text;
        return Task.CompletedTask;
    }
}

using FileCrypter.Desktop.Services;

namespace FileCrypter.Desktop.Tests.TestDoubles;

internal sealed class RecordingClipboardService : IClipboardService
{
    public string? LastText { get; private set; }

    public Exception? SetTextException { get; init; }

    public Task SetTextAsync(string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (SetTextException is not null)
        {
            return Task.FromException(SetTextException);
        }

        LastText = text;
        return Task.CompletedTask;
    }
}

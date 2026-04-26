using FileCrypter.App.Services;

namespace FileCrypter.Desktop.Tests.TestDoubles;

internal sealed class RecordingPathRevealService : IPathRevealService
{
    public string? LastPath { get; private set; }

    public Task TryRevealPathAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastPath = path;
        return Task.CompletedTask;
    }
}

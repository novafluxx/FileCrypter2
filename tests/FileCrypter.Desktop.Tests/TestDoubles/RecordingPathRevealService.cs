using FileCrypter.Desktop.Services;

namespace FileCrypter.Desktop.Tests.TestDoubles;

internal sealed class RecordingPathRevealService : IPathRevealService
{
    public string? LastPath { get; private set; }

    public bool Result { get; init; } = true;

    public Task<bool> TryRevealPathAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        LastPath = path;
        return Task.FromResult(Result);
    }
}

namespace FileCrypter.Desktop.Services;

public sealed class NoOpPathRevealService : IPathRevealService
{
    public Task<bool> TryRevealPathAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.FromResult(true);
    }
}

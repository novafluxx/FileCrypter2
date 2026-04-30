namespace FileCrypter.Desktop.Services;

public sealed class NoOpPathRevealService : IPathRevealService
{
    public Task TryRevealPathAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return Task.CompletedTask;
    }
}

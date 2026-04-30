namespace FileCrypter.Desktop.Services;

public interface IPathRevealService
{
    Task<bool> TryRevealPathAsync(string path, CancellationToken cancellationToken);
}

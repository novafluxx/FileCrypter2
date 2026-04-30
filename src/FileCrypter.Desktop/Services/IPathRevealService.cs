namespace FileCrypter.Desktop.Services;

public interface IPathRevealService
{
    Task TryRevealPathAsync(string path, CancellationToken cancellationToken);
}

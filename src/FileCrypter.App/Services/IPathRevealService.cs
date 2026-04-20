namespace FileCrypter.App.Services;

public interface IPathRevealService
{
    Task TryRevealPathAsync(string path, CancellationToken cancellationToken);
}

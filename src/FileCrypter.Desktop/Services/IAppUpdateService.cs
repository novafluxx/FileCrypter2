namespace FileCrypter.Desktop.Services;

public interface IAppUpdateService
{
    AppUpdateCheckResult GetCurrentStatus();

    Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken);
}

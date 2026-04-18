namespace FileCrypter.App.Services;

public interface IAppUpdateService
{
    AppUpdateCheckResult GetCurrentStatus();

    Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken);
}

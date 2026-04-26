namespace FileCrypter.App.Services;

public sealed class DevelopmentAppUpdateService : IAppUpdateService
{
    private static readonly AppUpdateCheckResult CurrentStatus = new(
        AppUpdateStatus.NotConfigured,
        "Update checks are not configured for this development build yet.",
        "This local desktop build does not have a release feed or in-app installer wired up yet. Keep using the package source or newer build you trust while the fuller update flow is still in progress.");

    public AppUpdateCheckResult GetCurrentStatus()
    {
        return CurrentStatus;
    }

    public Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        return Task.FromResult(
            CurrentStatus with
            {
                CheckedAt = DateTimeOffset.Now,
            });
    }
}

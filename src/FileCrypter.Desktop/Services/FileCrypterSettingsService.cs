using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.Services;

public sealed class FileCrypterSettingsService : IFileCrypterSettingsService
{
    private readonly FileCrypterSettingsStore settingsStore;

    public FileCrypterSettingsService(FileCrypterSettingsStore? settingsStore = null)
    {
        this.settingsStore = settingsStore ?? new FileCrypterSettingsStore();
    }

    public string SettingsPath => settingsStore.SettingsPath;

    public Task<FileCrypterSettings> LoadAsync(CancellationToken cancellationToken)
    {
        return settingsStore.LoadAsync(cancellationToken);
    }

    public Task SaveAsync(FileCrypterSettings settings, CancellationToken cancellationToken)
    {
        return settingsStore.SaveAsync(settings, cancellationToken);
    }
}

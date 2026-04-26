using FileCrypter.Core.Settings;

namespace FileCrypter.App.Services;

public interface IFileCrypterSettingsService
{
    string SettingsPath { get; }

    Task<FileCrypterSettings> LoadAsync(CancellationToken cancellationToken);

    Task SaveAsync(FileCrypterSettings settings, CancellationToken cancellationToken);
}

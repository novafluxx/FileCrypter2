using System.Text.Json;

namespace FileCrypter.Core.Settings;

public sealed class FileCrypterSettingsStore
{
    private readonly string settingsPath;

    public FileCrypterSettingsStore(string? settingsPath = null)
    {
        this.settingsPath = Path.GetFullPath(settingsPath ?? GetDefaultSettingsPath());
    }

    public string SettingsPath => settingsPath;

    public async Task<FileCrypterSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(settingsPath))
        {
            return new FileCrypterSettings();
        }

        try
        {
            await using FileStream input = new(
                settingsPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 4096,
                FileOptions.SequentialScan);
            FileCrypterSettings? settings = await JsonSerializer
                .DeserializeAsync(input, FileCrypterSettingsJsonContext.Default.FileCrypterSettings, cancellationToken)
                .ConfigureAwait(false);
            return (settings ?? new FileCrypterSettings()).Normalize();
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException($"The FileCrypter settings file is invalid: {settingsPath}", exception);
        }
    }

    public async Task SaveAsync(FileCrypterSettings settings, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(settings);
        settings = settings.Normalize();

        string? directory = Path.GetDirectoryName(settingsPath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        string stagingPath = CreateStagingPath(settingsPath);
        bool completed = false;
        try
        {
            await using (FileStream output = new(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 4096,
                FileOptions.SequentialScan))
            {
                await JsonSerializer
                    .SerializeAsync(output, settings, FileCrypterSettingsJsonContext.Default.FileCrypterSettings, cancellationToken)
                    .ConfigureAwait(false);
            }

            if (IsSymbolicLink(settingsPath))
            {
                throw new IOException("The settings path must not be a symbolic link.");
            }

            File.Move(stagingPath, settingsPath, overwrite: true);
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                TryDeleteFile(stagingPath);
            }
        }
    }

    private static string GetDefaultSettingsPath()
    {
        string applicationData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        if (!string.IsNullOrEmpty(applicationData))
        {
            return Path.Combine(applicationData, "FileCrypter", "settings.json");
        }

        string userProfile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrEmpty(userProfile))
        {
            return Path.Combine(userProfile, ".filecrypter", "settings.json");
        }

        return Path.Combine(Path.GetTempPath(), "FileCrypter", "settings.json");
    }

    private static string CreateStagingPath(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        string fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException("The settings path must include a file name.", nameof(path));
        }

        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static bool IsSymbolicLink(string path)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.LinkTarget is not null)
        {
            return true;
        }

        var directoryInfo = new DirectoryInfo(path);
        return directoryInfo.LinkTarget is not null;
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}

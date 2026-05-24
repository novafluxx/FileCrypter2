using System.Text.Json;

namespace FileCrypter.Core.Settings;

public sealed class FileCrypterSettingsStore
{
    private readonly string settingsPath;
    private readonly string settingsDirectory;

    public FileCrypterSettingsStore(string? settingsPath = null)
    {
        this.settingsPath = Path.GetFullPath(settingsPath ?? GetDefaultSettingsPath());
        this.settingsDirectory = ResolveSettingsDirectory(this.settingsPath);
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

        Directory.CreateDirectory(settingsDirectory);

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
                TryDeleteStagingFile(stagingPath, settingsDirectory);
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

        ValidateSafeFileName(fileName, nameof(path));

        return Path.Combine(directory, $".{fileName}.{Guid.NewGuid():N}.tmp");
    }

    private static string ResolveSettingsDirectory(string path)
    {
        string? directory = Path.GetDirectoryName(path);
        string fileName = Path.GetFileName(path);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            throw new ArgumentException("The settings path must include a directory and file name.", nameof(path));
        }

        ValidateSafeFileName(fileName, nameof(path));

        string fullDirectory = Path.GetFullPath(directory);
        string fullPath = Path.GetFullPath(Path.Combine(fullDirectory, fileName));
        if (!IsPathInsideDirectory(fullPath, fullDirectory))
        {
            throw new ArgumentException("The settings path must stay within its settings directory.", nameof(path));
        }

        return fullDirectory;
    }

    private static bool IsSymbolicLink(string path)
    {
        if (path == null || path.Contains(".."))
        {
            throw new ArgumentException("Invalid file path");
        }

        var fileInfo = new FileInfo(path);
        if (fileInfo.LinkTarget is not null)
        {
            return true;
        }

        var directoryInfo = new DirectoryInfo(path);
        return directoryInfo.LinkTarget is not null;
    }

    private static void TryDeleteStagingFile(string path, string expectedDirectory)
    {
        try
        {
            string fullPath = Path.GetFullPath(path);
            string fullExpectedDirectory = Path.GetFullPath(expectedDirectory);
            string fileName = Path.GetFileName(fullPath);
            if (!IsPathInsideDirectory(fullPath, fullExpectedDirectory)
                || !IsSafeFileName(fileName)
                || !fileName.StartsWith(".", StringComparison.Ordinal)
                || !fileName.EndsWith(".tmp", StringComparison.Ordinal))
            {
                return;
            }

            File.Delete(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static bool IsPathInsideDirectory(string path, string directory)
    {
        string comparisonDirectory = Path.TrimEndingDirectorySeparator(directory) + Path.DirectorySeparatorChar;
        string comparisonPath = Path.GetFullPath(path);
        return comparisonPath.StartsWith(comparisonDirectory, StringComparison.OrdinalIgnoreCase);
    }

    private static void ValidateSafeFileName(string fileName, string parameterName)
    {
        if (!IsSafeFileName(fileName))
        {
            throw new ArgumentException("The settings file name must not contain path traversal or directory separators.", parameterName);
        }
    }

    private static bool IsSafeFileName(string fileName)
    {
        return !string.IsNullOrWhiteSpace(fileName)
            && !fileName.Contains("..", StringComparison.Ordinal)
            && !fileName.Contains('/', StringComparison.Ordinal)
            && !fileName.Contains('\\', StringComparison.Ordinal);
    }
}

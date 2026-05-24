using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.Tests.Services;

public sealed class FileCrypterSettingsStoreTests
{
    [Fact]
    public async Task LoadAsync_MissingFile_UsesSystemThemeDefault()
    {
        using var tempDirectory = new TemporaryDirectory();
        string settingsPath = Path.Combine(tempDirectory.Path, "settings.json");
        var store = new FileCrypterSettingsStore(settingsPath);

        FileCrypterSettings settings = await store.LoadAsync();

        Assert.Equal(FileCrypterThemePreference.System, settings.ThemePreference);
        Assert.True(settings.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(string.Empty, settings.DefaultOutputDirectory);
    }

    [Fact]
    public async Task SaveAsync_AndLoadAsync_RoundTripThemePreference()
    {
        using var tempDirectory = new TemporaryDirectory();
        string settingsPath = Path.Combine(tempDirectory.Path, "settings.json");
        var store = new FileCrypterSettingsStore(settingsPath);
        FileCrypterSettings settings = new()
        {
            ThemePreference = FileCrypterThemePreference.Dark,
            EnableCompressionByDefault = true,
            NeverOverwriteExistingFilesByDefault = false,
            DefaultOutputDirectory = tempDirectory.Path,
        };

        await store.SaveAsync(settings);

        string json = await File.ReadAllTextAsync(settingsPath);
        Assert.Contains("\"themePreference\": \"dark\"", json, StringComparison.Ordinal);

        FileCrypterSettings loadedSettings = await store.LoadAsync();

        Assert.Equal(FileCrypterThemePreference.Dark, loadedSettings.ThemePreference);
        Assert.True(loadedSettings.EnableCompressionByDefault);
        Assert.False(loadedSettings.NeverOverwriteExistingFilesByDefault);
        Assert.Equal(tempDirectory.Path, loadedSettings.DefaultOutputDirectory);
    }

    [Fact]
    public async Task SaveAsync_WhenSettingsPathIsSymbolicLink_ThrowsAndPreservesTarget()
    {
        using var tempDirectory = new TemporaryDirectory();
        string targetPath = Path.Combine(tempDirectory.Path, "target.json");
        string settingsPath = Path.Combine(tempDirectory.Path, "settings.json");
        const string targetJson = """{"themePreference":"dark"}""";
        await File.WriteAllTextAsync(targetPath, targetJson);
        if (!TryCreateFileSymbolicLink(settingsPath, targetPath))
        {
            return;
        }

        var store = new FileCrypterSettingsStore(settingsPath);

        IOException exception = await Assert.ThrowsAsync<IOException>(
            () => store.SaveAsync(new FileCrypterSettings()));

        Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(targetJson, await File.ReadAllTextAsync(targetPath));
        Assert.True(File.Exists(settingsPath));
        Assert.Empty(Directory.GetFiles(tempDirectory.Path, "*.tmp"));
    }

    [Fact]
    public void Constructor_WhenSettingsFileNameContainsTraversalSegment_Throws()
    {
        using var tempDirectory = new TemporaryDirectory();
        string settingsPath = Path.Combine(tempDirectory.Path, "settings..json");

        ArgumentException exception = Assert.Throws<ArgumentException>(() => new FileCrypterSettingsStore(settingsPath));

        Assert.Contains("path traversal", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static bool TryCreateFileSymbolicLink(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                {
                    Directory.Delete(Path, recursive: true);
                }
            }
            catch
            {
            }
        }
    }
}

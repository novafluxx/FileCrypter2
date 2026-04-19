using FileCrypter.Core.Settings;

namespace FileCrypter.App.Tests.Services;

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

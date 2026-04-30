using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.Services;

public sealed class NoOpAppThemeService : IAppThemeService
{
    public void ApplyTheme(FileCrypterThemePreference preference)
    {
    }
}

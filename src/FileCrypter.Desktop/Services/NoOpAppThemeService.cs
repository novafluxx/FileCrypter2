using FileCrypter.Core.Settings;

namespace FileCrypter.App.Services;

public sealed class NoOpAppThemeService : IAppThemeService
{
    public void ApplyTheme(FileCrypterThemePreference preference)
    {
    }
}

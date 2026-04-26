using FileCrypter.Core.Settings;

namespace FileCrypter.App.Services;

public interface IAppThemeService
{
    void ApplyTheme(FileCrypterThemePreference preference);
}

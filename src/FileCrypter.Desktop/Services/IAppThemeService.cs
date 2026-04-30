using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.Services;

public interface IAppThemeService
{
    void ApplyTheme(FileCrypterThemePreference preference);
}

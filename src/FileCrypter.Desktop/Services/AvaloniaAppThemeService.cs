using Avalonia;
using Avalonia.Styling;
using Avalonia.Threading;
using FileCrypter.Core.Settings;

namespace FileCrypter.Desktop.Services;

public sealed class AvaloniaAppThemeService : IAppThemeService
{
    private readonly Application application;

    public AvaloniaAppThemeService(Application? application = null)
    {
        this.application = application ?? Application.Current
            ?? throw new InvalidOperationException("Avalonia Application.Current is not available.");
    }

    public void ApplyTheme(FileCrypterThemePreference preference)
    {
        ThemeVariant themeVariant = MapThemeVariant(preference);

        if (Dispatcher.UIThread.CheckAccess())
        {
            application.RequestedThemeVariant = themeVariant;
            return;
        }

        Dispatcher.UIThread.Post(() => application.RequestedThemeVariant = themeVariant);
    }

    internal static ThemeVariant MapThemeVariant(FileCrypterThemePreference preference)
    {
        FileCrypterThemePreference normalizedPreference = Enum.IsDefined(preference)
            ? preference
            : FileCrypterThemePreference.System;

        return normalizedPreference switch
        {
            FileCrypterThemePreference.Light => ThemeVariant.Light,
            FileCrypterThemePreference.Dark => ThemeVariant.Dark,
            _ => ThemeVariant.Default,
        };
    }
}

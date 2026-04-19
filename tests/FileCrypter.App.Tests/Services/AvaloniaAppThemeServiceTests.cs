using Avalonia;
using Avalonia.Styling;
using FileCrypter.App.Services;
using FileCrypter.Core.Settings;

namespace FileCrypter.App.Tests.Services;

public sealed class AvaloniaAppThemeServiceTests
{
    [Fact]
    public void ApplyTheme_MapsPreferencesToRequestedThemeVariant()
    {
        var application = new TestApplication();
        var service = new AvaloniaAppThemeService(application);

        service.ApplyTheme(FileCrypterThemePreference.System);
        Assert.Same(ThemeVariant.Default, application.RequestedThemeVariant);

        service.ApplyTheme(FileCrypterThemePreference.Light);
        Assert.Same(ThemeVariant.Light, application.RequestedThemeVariant);

        service.ApplyTheme(FileCrypterThemePreference.Dark);
        Assert.Same(ThemeVariant.Dark, application.RequestedThemeVariant);
    }

    private sealed class TestApplication : Application
    {
    }
}

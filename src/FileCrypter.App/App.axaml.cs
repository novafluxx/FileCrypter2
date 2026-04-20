using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Data.Core;
using Avalonia.Data.Core.Plugins;
using System.Linq;
using Avalonia.Markup.Xaml;
using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;
using FileCrypter.App.Views;

namespace FileCrypter.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var mainWindow = new MainWindow();
            mainWindow.DataContext = new MainWindowViewModel(
                new FileCrypterWorkflowService(),
                new FileCrypterSettingsService(),
                new AvaloniaFilePickerService(mainWindow),
                new AppMetadataService(),
                new DevelopmentAppUpdateService(),
                new AvaloniaAppThemeService(this),
                new AvaloniaClipboardService(mainWindow),
                new DesktopPathRevealService());
            desktop.MainWindow = mainWindow;
        }

        base.OnFrameworkInitializationCompleted();
    }
}

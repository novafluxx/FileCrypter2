using Avalonia.Controls;
using Avalonia.Controls.Templates;
using FileCrypter.Desktop.ViewModels;
using FileCrypter.Desktop.Views;

namespace FileCrypter.Desktop;

/// <summary>
/// Given a view model, returns the corresponding view if possible.
/// </summary>
public class ViewLocator : IDataTemplate
{
    public Control? Build(object? param)
    {
        if (param is null)
        {
            return null;
        }

        return param switch
        {
            EncryptViewModel => new EncryptView(),
            DecryptViewModel => new DecryptView(),
            BatchViewModel => new BatchView(),
            HelpViewModel => new HelpView(),
            SettingsViewModel => new SettingsView(),
            PlaceholderPageViewModel => new PlaceholderPageView(),
            _ => new TextBlock { Text = "Not Found: " + param.GetType().Name },
        };
    }

    public bool Match(object? data)
    {
        return data is ViewModelBase;
    }
}

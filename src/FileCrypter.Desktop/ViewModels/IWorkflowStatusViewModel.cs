using System.Windows.Input;

namespace FileCrypter.Desktop.ViewModels;

public interface IWorkflowStatusViewModel
{
    string StatusText { get; }

    string ProgressText { get; }

    string FooterActionText => string.Empty;

    ICommand? FooterActionCommand => null;

    bool HasFooterAction => FooterActionCommand is not null && !string.IsNullOrWhiteSpace(FooterActionText);
}

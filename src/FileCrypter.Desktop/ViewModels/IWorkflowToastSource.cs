namespace FileCrypter.Desktop.ViewModels;

public interface IWorkflowToastSource
{
    event EventHandler<WorkflowToastNotification>? ToastNotificationRequested;
}

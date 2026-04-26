namespace FileCrypter.App.ViewModels;

public interface IWorkflowToastSource
{
    event EventHandler<WorkflowToastNotification>? ToastNotificationRequested;
}

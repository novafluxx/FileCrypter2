namespace FileCrypter.Desktop.ViewModels;

public sealed record WorkflowToastNotification(
    WorkflowToastKind Kind,
    string Title,
    string Message,
    string Detail = "");

namespace FileCrypter.App.ViewModels;

public sealed record WorkflowToastNotification(
    WorkflowToastKind Kind,
    string Title,
    string Message,
    string Detail = "");

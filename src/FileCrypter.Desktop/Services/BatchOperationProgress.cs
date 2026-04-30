namespace FileCrypter.Desktop.Services;

public readonly record struct BatchOperationProgress(
    int CompletedFiles,
    int TotalFiles,
    string? CurrentInputPath,
    long CurrentInputBytes,
    long? CurrentTotalInputBytes,
    double Percent);

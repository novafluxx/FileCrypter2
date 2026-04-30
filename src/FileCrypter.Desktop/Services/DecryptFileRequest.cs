namespace FileCrypter.Desktop.Services;

public sealed record DecryptFileRequest(
    string SourcePath,
    string? OutputPath,
    string Password,
    bool NeverOverwriteExistingFiles,
    string? KeyFilePath);

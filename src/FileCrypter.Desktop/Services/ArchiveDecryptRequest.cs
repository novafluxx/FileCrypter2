namespace FileCrypter.Desktop.Services;

public sealed record ArchiveDecryptRequest(
    string SourcePath,
    string OutputDirectory,
    string Password,
    bool NeverOverwriteExistingFiles,
    string? KeyFilePath);

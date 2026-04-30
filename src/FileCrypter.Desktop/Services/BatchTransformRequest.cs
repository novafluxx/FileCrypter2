namespace FileCrypter.Desktop.Services;

public sealed record BatchTransformRequest(
    IReadOnlyList<string> SourcePaths,
    string OutputDirectory,
    string Password,
    bool NeverOverwriteExistingFiles,
    string? KeyFilePath);

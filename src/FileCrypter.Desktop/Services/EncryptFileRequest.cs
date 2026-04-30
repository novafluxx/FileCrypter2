namespace FileCrypter.Desktop.Services;

public sealed record EncryptFileRequest(
    string SourcePath,
    string? OutputPath,
    string Password,
    bool EnableCompression,
    bool NeverOverwriteExistingFiles,
    string? KeyFilePath,
    string? GenerateKeyFilePath);

namespace FileCrypter.Desktop.Services;

public sealed record EncryptFileResult(string OutputPath, string? GeneratedKeyFilePath);

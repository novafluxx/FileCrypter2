namespace FileCrypter.App.Services;

public sealed record EncryptFileResult(string OutputPath, string? GeneratedKeyFilePath);

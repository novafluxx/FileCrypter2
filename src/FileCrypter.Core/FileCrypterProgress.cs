namespace FileCrypter.Core;

public readonly record struct FileCrypterProgress(long InputBytes, long OutputBytes)
{
    public string? Phase { get; init; }

    public long? TotalInputBytes { get; init; }
}

public static class FileCrypterProgressPhases
{
    public const string CreatingArchive = "creating-archive";
    public const string EncryptingArchive = "encrypting-archive";
    public const string DecryptingArchive = "decrypting-archive";
    public const string ExtractingArchive = "extracting-archive";
}

namespace FileCrypter.Core;

/// <summary>
/// Reports FileCrypter operation progress as input and output byte counters.
/// </summary>
/// <param name="InputBytes">The number of bytes read from the active input stream, file, or archive phase.</param>
/// <param name="OutputBytes">The number of bytes written to the active output stream, file, or archive phase.</param>
/// <remarks>
/// Progress values are emitted during chunk encryption, chunk decryption, archive creation, archive encryption,
/// archive decryption, and archive extraction. A report can describe an intermediate state; callers should treat the
/// final operation result or exception as authoritative for success or failure.
/// </remarks>
public readonly record struct FileCrypterProgress(long InputBytes, long OutputBytes)
{
    /// <summary>
    /// Gets the optional archive phase associated with this progress report.
    /// </summary>
    /// <remarks>
    /// Single-file encryption and decryption usually leave this value unset. Archive operations use values from
    /// <see cref="FileCrypterProgressPhases"/>.
    /// </remarks>
    public string? Phase { get; init; }

    /// <summary>
    /// Gets the optional total number of input bytes expected for the current operation or archive phase.
    /// </summary>
    /// <remarks>
    /// This value can be unavailable when the underlying stream cannot provide a stable length.
    /// </remarks>
    public long? TotalInputBytes { get; init; }
}

/// <summary>
/// Contains phase names used in <see cref="FileCrypterProgress.Phase"/> for archive operations.
/// </summary>
public static class FileCrypterProgressPhases
{
    /// <summary>
    /// Indicates that plaintext files are being packed into a temporary tar archive.
    /// </summary>
    public const string CreatingArchive = "creating-archive";

    /// <summary>
    /// Indicates that the temporary tar archive is being encrypted.
    /// </summary>
    public const string EncryptingArchive = "encrypting-archive";

    /// <summary>
    /// Indicates that an encrypted archive is being decrypted into a temporary tar archive.
    /// </summary>
    public const string DecryptingArchive = "decrypting-archive";

    /// <summary>
    /// Indicates that files are being extracted from a decrypted temporary tar archive.
    /// </summary>
    public const string ExtractingArchive = "extracting-archive";
}

namespace FileCrypter.Core;

/// <summary>
/// Configures encryption, decryption, compression, key derivation, and progress reporting for FileCrypter operations.
/// </summary>
/// <remarks>
/// The defaults are intended for normal interactive use with the FileCrypter v1 format. Encryption writes the selected
/// chunk size, Argon2id parameters, and compression setting into the file header so decryption can validate and replay the
/// same format choices. Passwords and key-file material are supplied to the operation methods, not stored on this options
/// object.
/// </remarks>
public sealed class FileCrypterOptions
{
    /// <summary>
    /// Gets the plaintext chunk size, in bytes, used for AES-GCM framing.
    /// </summary>
    /// <remarks>
    /// The default is 1 MiB. Values must be between 64 KiB and 16 MiB, inclusive, and must be a multiple of 1 KiB.
    /// Larger chunks reduce frame overhead but require larger transient plaintext and ciphertext buffers.
    /// </remarks>
    public int ChunkSize { get; init; } = (int)Format.FileCrypterFormatConstants.DefaultChunkSize;

    /// <summary>
    /// Gets the Argon2id memory cost, in KiB, used when deriving the AES-256 key from the password and optional key file.
    /// </summary>
    /// <remarks>
    /// The default is 65,536 KiB. Values must be between 19,456 KiB and 1,048,576 KiB, inclusive.
    /// </remarks>
    public int Argon2MemoryKiB { get; init; } = (int)Format.FileCrypterFormatConstants.DefaultArgon2MemoryKiB;

    /// <summary>
    /// Gets the Argon2id iteration count used for password-based key derivation.
    /// </summary>
    /// <remarks>
    /// The default is 3. Values must be between 2 and 64, inclusive.
    /// </remarks>
    public int Argon2Iterations { get; init; } = (int)Format.FileCrypterFormatConstants.DefaultArgon2Iterations;

    /// <summary>
    /// Gets the Argon2id degree of parallelism used for password-based key derivation.
    /// </summary>
    /// <remarks>
    /// The default is 4. Values must be between 1 and 16, inclusive.
    /// </remarks>
    public int Argon2Parallelism { get; init; } = (int)Format.FileCrypterFormatConstants.DefaultArgon2Parallelism;

    /// <summary>
    /// Gets a value indicating whether single-file stream and path encryption compresses plaintext before encryption.
    /// </summary>
    /// <remarks>
    /// Compression uses Zstandard and is authenticated as part of the encrypted payload. Archive encryption and batch
    /// file encryption enable compression internally regardless of this value. Decryption reads the compression setting
    /// from the encrypted file header.
    /// </remarks>
    public bool EnableCompression { get; init; }

    /// <summary>
    /// Gets the optional progress sink that receives byte counters and archive phase information.
    /// </summary>
    /// <remarks>
    /// Progress reports are best-effort notifications emitted as chunks or archive entries are processed. The counters
    /// describe bytes read from the current input and bytes written to the current output for the active operation or
    /// phase; they are not a security boundary and should not be used to verify payload integrity.
    /// </remarks>
    public IProgress<FileCrypterProgress>? Progress { get; init; }

    internal IFileCrypterRandomSource? RandomSource { get; init; }
}

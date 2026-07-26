using FileCrypter.Core.Format;

namespace FileCrypter.Core;

/// <summary>
/// Describes the password-independent metadata recorded in a FileCrypter encrypted file header.
/// </summary>
/// <remarks>
/// Instances are produced by <see cref="FileCrypter.InspectAsync"/> and carried by
/// <see cref="FileCrypterVerifyResult"/>; hosts cannot construct one. The projection deliberately omits the header salt
/// and nonce prefix: they are format-level key-derivation and framing inputs with no host use, and exposing them only
/// widens what a host may accidentally log or display. Every value here is read from the header before any key
/// derivation happens, so it is metadata a possessor of the file already has and must not be treated as evidence that
/// the payload is authentic. Only <see cref="FileCrypter.VerifyFileAsync(string, string, FileCrypterOptions, CancellationToken)"/>
/// or a full decryption authenticates the ciphertext.
/// </remarks>
public sealed record FileCrypterFileInfo
{
    internal FileCrypterFileInfo(FileCrypterHeader header)
    {
        FormatVersion = header.FormatVersion;
        PayloadKindId = header.PayloadKind;
        PayloadKind = header.PayloadKind == FileCrypterFormatConstants.PayloadKindTarArchive
            ? FileCrypterPayloadKind.TarArchive
            : FileCrypterPayloadKind.SingleFile;
        CompressionAlgorithm = header.CompressionAlgorithmId == FileCrypterFormatConstants.CompressionZstd
            ? FileCrypterCompressionAlgorithm.Zstd
            : FileCrypterCompressionAlgorithm.None;
        IsKeyFileRequired = header.IsKeyFileRequired;
        ChunkSize = checked((int)header.ChunkSize);
        Argon2MemoryKiB = checked((int)header.Argon2MemoryKiB);
        Argon2Iterations = checked((int)header.Argon2Iterations);
        Argon2Parallelism = checked((int)header.Argon2Parallelism);
    }

    /// <summary>
    /// Gets the FileCrypter file format version recorded in the header.
    /// </summary>
    /// <remarks>
    /// Only version 1 is supported; headers declaring any other version are rejected before this projection is created.
    /// </remarks>
    public int FormatVersion { get; }

    /// <summary>
    /// Gets the kind of plaintext payload stored in the encrypted file.
    /// </summary>
    /// <remarks>
    /// Hosts should use this to route the file to single-file or archive decryption rather than guessing from the file
    /// name suffix, which is only a convention.
    /// </remarks>
    public FileCrypterPayloadKind PayloadKind { get; }

    /// <summary>
    /// Gets the compression algorithm applied to the payload before encryption.
    /// </summary>
    /// <remarks>
    /// Decryption replays this setting automatically; it is exposed for display only.
    /// </remarks>
    public FileCrypterCompressionAlgorithm CompressionAlgorithm { get; }

    /// <summary>
    /// Gets a value indicating whether decryption requires the matching key file in addition to the password.
    /// </summary>
    /// <remarks>
    /// This reflects the header key-file flag only. It does not identify which key file is required, and a matching key
    /// file can only be confirmed by a successful authenticated decryption or verification.
    /// </remarks>
    public bool IsKeyFileRequired { get; }

    /// <summary>
    /// Gets the plaintext chunk size, in bytes, used for AES-GCM framing of the payload.
    /// </summary>
    /// <remarks>
    /// The header value is bounded to the supported FileCrypter v1 range before parsing succeeds, so this is always
    /// between 64 KiB and 16 MiB and a multiple of 1 KiB.
    /// </remarks>
    public int ChunkSize { get; }

    /// <summary>
    /// Gets the Argon2id memory cost, in KiB, that decryption will use to derive the key.
    /// </summary>
    /// <remarks>
    /// The header value is bounded before parsing succeeds. Hosts can use it to warn that decrypting a file will need
    /// this much memory.
    /// </remarks>
    public int Argon2MemoryKiB { get; }

    /// <summary>
    /// Gets the Argon2id iteration count that decryption will use to derive the key.
    /// </summary>
    /// <remarks>
    /// The header value is bounded before parsing succeeds.
    /// </remarks>
    public int Argon2Iterations { get; }

    /// <summary>
    /// Gets the Argon2id degree of parallelism that decryption will use to derive the key.
    /// </summary>
    /// <remarks>
    /// The header value is bounded before parsing succeeds.
    /// </remarks>
    public int Argon2Parallelism { get; }

    internal byte PayloadKindId { get; }
}

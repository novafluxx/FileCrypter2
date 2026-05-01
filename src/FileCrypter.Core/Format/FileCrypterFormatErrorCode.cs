namespace FileCrypter.Core.Format;

/// <summary>
/// Identifies the reason a FileCrypter payload could not be parsed, authenticated, decompressed, or extracted.
/// </summary>
/// <remarks>
/// These values are intended for host applications that need to map format failures to stable user-facing guidance.
/// The enum does not describe ordinary file-system, cancellation, or argument validation failures.
/// </remarks>
public enum FileCrypterFormatErrorCode
{
    /// <summary>
    /// The input ended before a complete FileCrypter header could be read.
    /// </summary>
    TruncatedHeader,

    /// <summary>
    /// The header magic bytes do not identify a FileCrypter payload.
    /// </summary>
    InvalidMagic,

    /// <summary>
    /// The payload version is not supported by this implementation.
    /// </summary>
    UnsupportedVersion,

    /// <summary>
    /// The encoded header length is not valid for the supported FileCrypter format.
    /// </summary>
    InvalidHeaderLength,

    /// <summary>
    /// The header contains unsupported or inconsistent flags.
    /// </summary>
    InvalidFlags,

    /// <summary>
    /// The header requests an unsupported authenticated encryption algorithm.
    /// </summary>
    UnsupportedAeadAlgorithm,

    /// <summary>
    /// The header requests an unsupported key derivation algorithm.
    /// </summary>
    UnsupportedKdfAlgorithm,

    /// <summary>
    /// The header requests an unsupported compression algorithm.
    /// </summary>
    UnsupportedCompressionAlgorithm,

    /// <summary>
    /// The payload kind is unsupported or does not match the requested operation.
    /// </summary>
    UnsupportedPayloadKind,

    /// <summary>
    /// The encoded chunk size is outside the supported FileCrypter range.
    /// </summary>
    InvalidChunkSize,

    /// <summary>
    /// The encoded Argon2id parameters are outside the supported FileCrypter range.
    /// </summary>
    InvalidArgon2Parameters,

    /// <summary>
    /// The encoded Argon2 version is not supported by this implementation.
    /// </summary>
    UnsupportedArgon2Version,

    /// <summary>
    /// The header identifies an invalid key-file hash algorithm.
    /// </summary>
    InvalidKeyFileHashAlgorithm,

    /// <summary>
    /// Reserved header bytes contain nonzero data.
    /// </summary>
    InvalidReservedBytes,

    /// <summary>
    /// The header key-file requirement is unsupported for the requested operation.
    /// </summary>
    UnsupportedKeyFileRequirement,

    /// <summary>
    /// The payload requires a key file, but no key-file material was supplied.
    /// </summary>
    KeyFileRequired,

    /// <summary>
    /// The input ended before a complete encrypted chunk could be read.
    /// </summary>
    TruncatedChunk,

    /// <summary>
    /// A chunk contains unsupported flags.
    /// </summary>
    InvalidChunkFlags,

    /// <summary>
    /// A chunk length is inconsistent with the header chunk size or final-chunk rules.
    /// </summary>
    InvalidChunkLength,

    /// <summary>
    /// Reserved chunk bytes contain nonzero data.
    /// </summary>
    InvalidChunkReservedBytes,

    /// <summary>
    /// The payload exceeds the FileCrypter v1 chunk counter capacity.
    /// </summary>
    TooManyChunks,

    /// <summary>
    /// AES-GCM authentication failed, usually because the password, key file, or encrypted payload is wrong or corrupted.
    /// </summary>
    AuthenticationFailed,

    /// <summary>
    /// Extra data was found after the final encrypted chunk.
    /// </summary>
    TrailingData,

    /// <summary>
    /// The authenticated compressed payload could not be decompressed.
    /// </summary>
    InvalidCompressedPayload,

    /// <summary>
    /// The authenticated archive payload is not a supported, safe FileCrypter tar archive.
    /// </summary>
    InvalidArchivePayload,
}

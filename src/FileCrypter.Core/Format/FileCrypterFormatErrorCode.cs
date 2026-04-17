namespace FileCrypter.Core.Format;

public enum FileCrypterFormatErrorCode
{
    TruncatedHeader,
    InvalidMagic,
    UnsupportedVersion,
    InvalidHeaderLength,
    InvalidFlags,
    UnsupportedAeadAlgorithm,
    UnsupportedKdfAlgorithm,
    UnsupportedCompressionAlgorithm,
    UnsupportedPayloadKind,
    InvalidChunkSize,
    InvalidArgon2Parameters,
    UnsupportedArgon2Version,
    InvalidKeyFileHashAlgorithm,
    InvalidReservedBytes,
    UnsupportedKeyFileRequirement,
    TruncatedChunk,
    InvalidChunkFlags,
    InvalidChunkLength,
    InvalidChunkReservedBytes,
    TooManyChunks,
    AuthenticationFailed,
    TrailingData,
}

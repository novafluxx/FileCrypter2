namespace FileCrypter.Core.Format;

internal enum FileCrypterFormatErrorCode
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
}

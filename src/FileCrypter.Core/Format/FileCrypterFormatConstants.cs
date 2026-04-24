namespace FileCrypter.Core.Format;

internal static class FileCrypterFormatConstants
{
    public static ReadOnlySpan<byte> Magic => "FCRYPT\r\n"u8;

    public const ushort Version = 1;
    public const ushort HeaderLength = 64;

    public const ushort HeaderFlagKeyFileRequired = 0x0001;
    public const ushort KnownHeaderFlags = HeaderFlagKeyFileRequired;

    public const byte AeadAlgorithmAes256Gcm = 1;
    public const byte KdfAlgorithmArgon2Id = 1;

    public const byte CompressionNone = 0;
    public const byte CompressionZstd = 1;

    public const byte PayloadKindSingleFile = 1;
    public const byte PayloadKindTarArchive = 2;

    public const int SaltLength = 16;
    public const int NoncePrefixLength = 8;
    public const int AesGcmNonceLength = 12;
    public const int AesGcmTagLength = 16;
    public const int DerivedKeyLength = 32;
    public const int ChunkFramePrefixLength = 8;

    public const uint DefaultChunkSize = 1_048_576;
    public const uint MinimumChunkSize = 65_536;
    public const uint MaximumChunkSize = 16_777_216;
    public const uint ChunkSizeMultiple = 1_024;

    public const uint DefaultArgon2MemoryKiB = 65_536;
    public const uint DefaultArgon2Iterations = 3;
    public const uint DefaultArgon2Parallelism = 4;
    public const uint MinimumArgon2MemoryKiB = 19_456;
    public const uint MaximumArgon2MemoryKiB = 1_048_576;
    public const uint MinimumArgon2Iterations = 2;
    public const uint MaximumArgon2Iterations = 64;
    public const uint MinimumArgon2Parallelism = 1;
    public const uint MaximumArgon2Parallelism = 16;
    public const byte Argon2Version = 0x13;

    public const byte KeyFileHashNone = 0;
    public const byte KeyFileHashSha256 = 1;

    public const ushort ChunkFlagFinal = 0x0001;
    public const ushort KnownChunkFlags = ChunkFlagFinal;

    public const long MaximumKeyFileSizeBytes = 16L * 1_024L * 1_024L;

    public const int MagicOffset = 0;
    public const int VersionOffset = 8;
    public const int HeaderLengthOffset = 10;
    public const int HeaderFlagsOffset = 12;
    public const int AeadAlgorithmOffset = 14;
    public const int KdfAlgorithmOffset = 15;
    public const int CompressionAlgorithmOffset = 16;
    public const int PayloadKindOffset = 17;
    public const int ChunkSizeOffset = 18;
    public const int SaltOffset = 22;
    public const int NoncePrefixOffset = 38;
    public const int Argon2MemoryOffset = 46;
    public const int Argon2IterationsOffset = 50;
    public const int Argon2ParallelismOffset = 54;
    public const int Argon2VersionOffset = 58;
    public const int KeyFileHashAlgorithmOffset = 59;
    public const int ReservedOffset = 60;
    public const int ReservedLength = 4;
}

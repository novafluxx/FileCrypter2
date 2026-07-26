using System.Buffers.Binary;

namespace FileCrypter.Core.Format;

internal static class FileCrypterHeaderParser
{
    public static FileCrypterHeader Parse(ReadOnlySpan<byte> header)
    {
        int availableMagicLength = Math.Min(header.Length, FileCrypterFormatConstants.Magic.Length);
        if (availableMagicLength == 0 ||
            !header[..availableMagicLength].SequenceEqual(FileCrypterFormatConstants.Magic[..availableMagicLength]))
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidMagic,
                "The input is not a FileCrypter encrypted file.");
        }

        if (header.Length < FileCrypterFormatConstants.HeaderLength)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.TruncatedHeader,
                "The FileCrypter header is truncated.");
        }

        ushort version = ReadUInt16(header, FileCrypterFormatConstants.VersionOffset);
        if (version != FileCrypterFormatConstants.Version)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedVersion,
                "The FileCrypter format version is not supported.");
        }

        ushort headerLength = ReadUInt16(header, FileCrypterFormatConstants.HeaderLengthOffset);
        if (headerLength != FileCrypterFormatConstants.HeaderLength)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidHeaderLength,
                "The FileCrypter header length is invalid.");
        }

        ushort headerFlags = ReadUInt16(header, FileCrypterFormatConstants.HeaderFlagsOffset);
        if ((headerFlags & ~FileCrypterFormatConstants.KnownHeaderFlags) != 0)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidFlags,
                "The FileCrypter header contains unsupported flags.");
        }

        byte aeadAlgorithmId = header[FileCrypterFormatConstants.AeadAlgorithmOffset];
        if (aeadAlgorithmId != FileCrypterFormatConstants.AeadAlgorithmAes256Gcm)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedAeadAlgorithm,
                "The FileCrypter AEAD algorithm is not supported.");
        }

        byte kdfAlgorithmId = header[FileCrypterFormatConstants.KdfAlgorithmOffset];
        if (kdfAlgorithmId != FileCrypterFormatConstants.KdfAlgorithmArgon2Id)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedKdfAlgorithm,
                "The FileCrypter KDF algorithm is not supported.");
        }

        byte compressionAlgorithmId = header[FileCrypterFormatConstants.CompressionAlgorithmOffset];
        if (compressionAlgorithmId is not FileCrypterFormatConstants.CompressionNone and not FileCrypterFormatConstants.CompressionZstd)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedCompressionAlgorithm,
                "The FileCrypter compression algorithm is not supported.");
        }

        byte payloadKind = header[FileCrypterFormatConstants.PayloadKindOffset];
        if (payloadKind is not FileCrypterFormatConstants.PayloadKindSingleFile and not FileCrypterFormatConstants.PayloadKindTarArchive)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedPayloadKind,
                "The FileCrypter payload kind is not supported.");
        }

        uint chunkSize = ReadUInt32(header, FileCrypterFormatConstants.ChunkSizeOffset);
        if (chunkSize < FileCrypterFormatConstants.MinimumChunkSize ||
            chunkSize > FileCrypterFormatConstants.MaximumChunkSize ||
            chunkSize % FileCrypterFormatConstants.ChunkSizeMultiple != 0)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidChunkSize,
                "The FileCrypter chunk size is invalid.");
        }

        uint argon2MemoryKiB = ReadUInt32(header, FileCrypterFormatConstants.Argon2MemoryOffset);
        uint argon2Iterations = ReadUInt32(header, FileCrypterFormatConstants.Argon2IterationsOffset);
        uint argon2Parallelism = ReadUInt32(header, FileCrypterFormatConstants.Argon2ParallelismOffset);
        if (argon2MemoryKiB < FileCrypterFormatConstants.MinimumArgon2MemoryKiB ||
            argon2MemoryKiB > FileCrypterFormatConstants.MaximumArgon2MemoryKiB ||
            argon2Iterations < FileCrypterFormatConstants.MinimumArgon2Iterations ||
            argon2Iterations > FileCrypterFormatConstants.MaximumArgon2Iterations ||
            argon2Parallelism < FileCrypterFormatConstants.MinimumArgon2Parallelism ||
            argon2Parallelism > FileCrypterFormatConstants.MaximumArgon2Parallelism)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidArgon2Parameters,
                "The FileCrypter Argon2 parameters are invalid.");
        }

        byte argon2Version = header[FileCrypterFormatConstants.Argon2VersionOffset];
        if (argon2Version != FileCrypterFormatConstants.Argon2Version)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedArgon2Version,
                "The FileCrypter Argon2 version is not supported.");
        }

        byte keyFileHashAlgorithmId = header[FileCrypterFormatConstants.KeyFileHashAlgorithmOffset];
        bool isKeyFileRequired = (headerFlags & FileCrypterFormatConstants.HeaderFlagKeyFileRequired) != 0;
        bool isKeyFileHashValid = isKeyFileRequired
            ? keyFileHashAlgorithmId == FileCrypterFormatConstants.KeyFileHashSha256
            : keyFileHashAlgorithmId == FileCrypterFormatConstants.KeyFileHashNone;

        if (!isKeyFileHashValid)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidKeyFileHashAlgorithm,
                "The FileCrypter key-file hash metadata is invalid.");
        }

        ReadOnlySpan<byte> reserved = header.Slice(
            FileCrypterFormatConstants.ReservedOffset,
            FileCrypterFormatConstants.ReservedLength);

        if (!reserved.SequenceEqual(stackalloc byte[FileCrypterFormatConstants.ReservedLength]))
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidReservedBytes,
                "The FileCrypter header contains nonzero reserved bytes.");
        }

        return new FileCrypterHeader(
            version,
            headerLength,
            headerFlags,
            aeadAlgorithmId,
            kdfAlgorithmId,
            compressionAlgorithmId,
            payloadKind,
            chunkSize,
            header.Slice(FileCrypterFormatConstants.SaltOffset, FileCrypterFormatConstants.SaltLength),
            header.Slice(FileCrypterFormatConstants.NoncePrefixOffset, FileCrypterFormatConstants.NoncePrefixLength),
            argon2MemoryKiB,
            argon2Iterations,
            argon2Parallelism,
            argon2Version,
            keyFileHashAlgorithmId);
    }

    private static ushort ReadUInt16(ReadOnlySpan<byte> header, int offset)
    {
        return BinaryPrimitives.ReadUInt16LittleEndian(header.Slice(offset, sizeof(ushort)));
    }

    private static uint ReadUInt32(ReadOnlySpan<byte> header, int offset)
    {
        return BinaryPrimitives.ReadUInt32LittleEndian(header.Slice(offset, sizeof(uint)));
    }
}

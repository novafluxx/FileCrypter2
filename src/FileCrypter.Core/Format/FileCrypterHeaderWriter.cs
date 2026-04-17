using System.Buffers.Binary;

namespace FileCrypter.Core.Format;

internal static class FileCrypterHeaderWriter
{
    public static void WritePasswordOnly(
        Span<byte> destination,
        FileCrypterOptions options,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> noncePrefix)
    {
        Write(destination, options, salt, noncePrefix, keyFileRequired: false);
    }

    public static void WriteKeyFileRequired(
        Span<byte> destination,
        FileCrypterOptions options,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> noncePrefix)
    {
        Write(destination, options, salt, noncePrefix, keyFileRequired: true);
    }

    private static void Write(
        Span<byte> destination,
        FileCrypterOptions options,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> noncePrefix,
        bool keyFileRequired)
    {
        if (destination.Length < FileCrypterFormatConstants.HeaderLength)
        {
            throw new ArgumentException("The destination is too small for a FileCrypter header.", nameof(destination));
        }

        if (salt.Length != FileCrypterFormatConstants.SaltLength)
        {
            throw new ArgumentException("The salt length is invalid.", nameof(salt));
        }

        if (noncePrefix.Length != FileCrypterFormatConstants.NoncePrefixLength)
        {
            throw new ArgumentException("The nonce prefix length is invalid.", nameof(noncePrefix));
        }

        destination.Slice(0, FileCrypterFormatConstants.HeaderLength).Clear();
        FileCrypterFormatConstants.Magic.CopyTo(destination);
        WriteUInt16(destination, FileCrypterFormatConstants.VersionOffset, FileCrypterFormatConstants.Version);
        WriteUInt16(destination, FileCrypterFormatConstants.HeaderLengthOffset, FileCrypterFormatConstants.HeaderLength);
        WriteUInt16(
            destination,
            FileCrypterFormatConstants.HeaderFlagsOffset,
            keyFileRequired ? FileCrypterFormatConstants.HeaderFlagKeyFileRequired : (ushort)0);
        destination[FileCrypterFormatConstants.AeadAlgorithmOffset] = FileCrypterFormatConstants.AeadAlgorithmAes256Gcm;
        destination[FileCrypterFormatConstants.KdfAlgorithmOffset] = FileCrypterFormatConstants.KdfAlgorithmArgon2Id;
        destination[FileCrypterFormatConstants.CompressionAlgorithmOffset] = FileCrypterFormatConstants.CompressionNone;
        destination[FileCrypterFormatConstants.PayloadKindOffset] = FileCrypterFormatConstants.PayloadKindSingleFile;
        WriteUInt32(destination, FileCrypterFormatConstants.ChunkSizeOffset, (uint)options.ChunkSize);
        salt.CopyTo(destination.Slice(FileCrypterFormatConstants.SaltOffset, FileCrypterFormatConstants.SaltLength));
        noncePrefix.CopyTo(destination.Slice(FileCrypterFormatConstants.NoncePrefixOffset, FileCrypterFormatConstants.NoncePrefixLength));
        WriteUInt32(destination, FileCrypterFormatConstants.Argon2MemoryOffset, (uint)options.Argon2MemoryKiB);
        WriteUInt32(destination, FileCrypterFormatConstants.Argon2IterationsOffset, (uint)options.Argon2Iterations);
        WriteUInt32(destination, FileCrypterFormatConstants.Argon2ParallelismOffset, (uint)options.Argon2Parallelism);
        destination[FileCrypterFormatConstants.Argon2VersionOffset] = FileCrypterFormatConstants.Argon2Version;
        destination[FileCrypterFormatConstants.KeyFileHashAlgorithmOffset] = keyFileRequired
            ? FileCrypterFormatConstants.KeyFileHashSha256
            : FileCrypterFormatConstants.KeyFileHashNone;
    }

    private static void WriteUInt16(Span<byte> destination, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(Span<byte> destination, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(offset, sizeof(uint)), value);
    }
}

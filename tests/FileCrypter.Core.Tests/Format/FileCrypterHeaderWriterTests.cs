using System.Buffers.Binary;
using FileCrypter.Core.Format;

namespace FileCrypter.Core.Tests.Format;

public sealed class FileCrypterHeaderWriterTests
{
    [Fact]
    public void WritePasswordOnly_WritesParseableHeader()
    {
        var options = new FileCrypterOptions
        {
            ChunkSize = (int)FileCrypterFormatConstants.MinimumChunkSize,
            Argon2MemoryKiB = 1024,
            Argon2Iterations = 1,
            Argon2Parallelism = 1,
        };
        byte[] salt = Enumerable.Range(1, FileCrypterFormatConstants.SaltLength).Select(value => (byte)value).ToArray();
        byte[] noncePrefix = Enumerable.Range(101, FileCrypterFormatConstants.NoncePrefixLength).Select(value => (byte)value).ToArray();
        byte[] headerBytes = new byte[FileCrypterFormatConstants.HeaderLength];

        FileCrypterHeaderWriter.WritePasswordOnly(headerBytes, options, salt, noncePrefix);

        FileCrypterHeader header = FileCrypterHeaderParser.Parse(headerBytes);
        Assert.Equal(FileCrypterFormatConstants.Version, header.FormatVersion);
        Assert.Equal(FileCrypterFormatConstants.HeaderLength, header.HeaderLength);
        Assert.Equal(0, header.HeaderFlags);
        Assert.Equal(FileCrypterFormatConstants.AeadAlgorithmAes256Gcm, header.AeadAlgorithmId);
        Assert.Equal(FileCrypterFormatConstants.KdfAlgorithmArgon2Id, header.KdfAlgorithmId);
        Assert.Equal(FileCrypterFormatConstants.CompressionNone, header.CompressionAlgorithmId);
        Assert.Equal(FileCrypterFormatConstants.PayloadKindSingleFile, header.PayloadKind);
        Assert.Equal(options.ChunkSize, (int)header.ChunkSize);
        Assert.Equal(options.Argon2MemoryKiB, (int)header.Argon2MemoryKiB);
        Assert.Equal(options.Argon2Iterations, (int)header.Argon2Iterations);
        Assert.Equal(options.Argon2Parallelism, (int)header.Argon2Parallelism);
        Assert.Equal(FileCrypterFormatConstants.Argon2Version, header.Argon2Version);
        Assert.Equal(FileCrypterFormatConstants.KeyFileHashNone, header.KeyFileHashAlgorithmId);
        Assert.Equal(salt, header.Salt.ToArray());
        Assert.Equal(noncePrefix, header.NoncePrefix.ToArray());
        Assert.Equal(0u, BinaryPrimitives.ReadUInt32LittleEndian(headerBytes.AsSpan(FileCrypterFormatConstants.ReservedOffset)));
    }
}

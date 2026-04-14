using System.Buffers.Binary;
using FileCrypter.Core.Format;

namespace FileCrypter.Core.Tests.Format;

public sealed class FileCrypterHeaderParserTests
{
    [Fact]
    public void Parse_WithValidPasswordOnlyHeader_ReturnsHeader()
    {
        byte[] headerBytes = CreateValidHeader();

        FileCrypterHeader header = FileCrypterHeaderParser.Parse(headerBytes);

        Assert.Equal(FileCrypterFormatConstants.Version, header.FormatVersion);
        Assert.Equal(FileCrypterFormatConstants.HeaderLength, header.HeaderLength);
        Assert.Equal(0, header.HeaderFlags);
        Assert.False(header.IsKeyFileRequired);
        Assert.Equal(FileCrypterFormatConstants.AeadAlgorithmAes256Gcm, header.AeadAlgorithmId);
        Assert.Equal(FileCrypterFormatConstants.KdfAlgorithmArgon2Id, header.KdfAlgorithmId);
        Assert.Equal(FileCrypterFormatConstants.CompressionNone, header.CompressionAlgorithmId);
        Assert.Equal(FileCrypterFormatConstants.PayloadKindSingleFile, header.PayloadKind);
        Assert.Equal(FileCrypterFormatConstants.DefaultChunkSize, header.ChunkSize);
        Assert.Equal(FileCrypterFormatConstants.DefaultArgon2MemoryKiB, header.Argon2MemoryKiB);
        Assert.Equal(FileCrypterFormatConstants.DefaultArgon2Iterations, header.Argon2Iterations);
        Assert.Equal(FileCrypterFormatConstants.DefaultArgon2Parallelism, header.Argon2Parallelism);
        Assert.Equal(FileCrypterFormatConstants.Argon2Version, header.Argon2Version);
        Assert.Equal(FileCrypterFormatConstants.KeyFileHashNone, header.KeyFileHashAlgorithmId);
        Assert.Equal(CreateSalt(), header.Salt.ToArray());
        Assert.Equal(CreateNoncePrefix(), header.NoncePrefix.ToArray());
    }

    [Fact]
    public void Parse_WithValidKeyFileRequiredHeader_ReturnsHeader()
    {
        byte[] headerBytes = CreateValidHeader(keyFileRequired: true);

        FileCrypterHeader header = FileCrypterHeaderParser.Parse(headerBytes);

        Assert.True(header.IsKeyFileRequired);
        Assert.Equal(FileCrypterFormatConstants.HeaderFlagKeyFileRequired, header.HeaderFlags);
        Assert.Equal(FileCrypterFormatConstants.KeyFileHashSha256, header.KeyFileHashAlgorithmId);
    }

    [Fact]
    public void Parse_CopiesSaltAndNoncePrefix()
    {
        byte[] headerBytes = CreateValidHeader();

        FileCrypterHeader header = FileCrypterHeaderParser.Parse(headerBytes);
        headerBytes[FileCrypterFormatConstants.SaltOffset] = 0xFF;
        headerBytes[FileCrypterFormatConstants.NoncePrefixOffset] = 0xFF;

        Assert.Equal(CreateSalt(), header.Salt.ToArray());
        Assert.Equal(CreateNoncePrefix(), header.NoncePrefix.ToArray());
    }

    [Fact]
    public void Parse_WithInvalidMagic_ThrowsInvalidMagic()
    {
        byte[] headerBytes = CreateValidHeader();
        headerBytes[0] = (byte)'X';

        AssertFormatError(FileCrypterFormatErrorCode.InvalidMagic, headerBytes);
    }

    [Fact]
    public void Parse_WithUnsupportedVersion_ThrowsUnsupportedVersion()
    {
        byte[] headerBytes = CreateValidHeader();
        WriteUInt16(headerBytes, FileCrypterFormatConstants.VersionOffset, 2);

        AssertFormatError(FileCrypterFormatErrorCode.UnsupportedVersion, headerBytes);
    }

    [Fact]
    public void Parse_WithTruncatedHeader_ThrowsTruncatedHeader()
    {
        byte[] headerBytes = CreateValidHeader();
        Array.Resize(ref headerBytes, FileCrypterFormatConstants.HeaderLength - 1);

        AssertFormatError(FileCrypterFormatErrorCode.TruncatedHeader, headerBytes);
    }

    [Fact]
    public void Parse_WithIncorrectHeaderLength_ThrowsInvalidHeaderLength()
    {
        byte[] headerBytes = CreateValidHeader();
        WriteUInt16(headerBytes, FileCrypterFormatConstants.HeaderLengthOffset, FileCrypterFormatConstants.HeaderLength + 1);

        AssertFormatError(FileCrypterFormatErrorCode.InvalidHeaderLength, headerBytes);
    }

    [Fact]
    public void Parse_WithUnknownFlags_ThrowsInvalidFlags()
    {
        byte[] headerBytes = CreateValidHeader();
        WriteUInt16(headerBytes, FileCrypterFormatConstants.HeaderFlagsOffset, 0x0002);

        AssertFormatError(FileCrypterFormatErrorCode.InvalidFlags, headerBytes);
    }

    [Theory]
    [InlineData(FileCrypterFormatConstants.AeadAlgorithmOffset, 2, (int)FileCrypterFormatErrorCode.UnsupportedAeadAlgorithm)]
    [InlineData(FileCrypterFormatConstants.KdfAlgorithmOffset, 2, (int)FileCrypterFormatErrorCode.UnsupportedKdfAlgorithm)]
    [InlineData(FileCrypterFormatConstants.CompressionAlgorithmOffset, 2, (int)FileCrypterFormatErrorCode.UnsupportedCompressionAlgorithm)]
    [InlineData(FileCrypterFormatConstants.PayloadKindOffset, 3, (int)FileCrypterFormatErrorCode.UnsupportedPayloadKind)]
    public void Parse_WithUnsupportedIds_ThrowsExpectedError(
        int offset,
        byte value,
        int expectedCode)
    {
        byte[] headerBytes = CreateValidHeader();
        headerBytes[offset] = value;

        AssertFormatError((FileCrypterFormatErrorCode)expectedCode, headerBytes);
    }

    [Theory]
    [InlineData(FileCrypterFormatConstants.MinimumChunkSize - FileCrypterFormatConstants.ChunkSizeMultiple)]
    [InlineData(FileCrypterFormatConstants.MaximumChunkSize + FileCrypterFormatConstants.ChunkSizeMultiple)]
    [InlineData(FileCrypterFormatConstants.DefaultChunkSize + 1)]
    public void Parse_WithInvalidChunkSize_ThrowsInvalidChunkSize(uint chunkSize)
    {
        byte[] headerBytes = CreateValidHeader();
        WriteUInt32(headerBytes, FileCrypterFormatConstants.ChunkSizeOffset, chunkSize);

        AssertFormatError(FileCrypterFormatErrorCode.InvalidChunkSize, headerBytes);
    }

    [Theory]
    [InlineData(FileCrypterFormatConstants.Argon2MemoryOffset)]
    [InlineData(FileCrypterFormatConstants.Argon2IterationsOffset)]
    [InlineData(FileCrypterFormatConstants.Argon2ParallelismOffset)]
    public void Parse_WithInvalidArgon2Parameters_ThrowsInvalidArgon2Parameters(int offset)
    {
        byte[] headerBytes = CreateValidHeader();
        WriteUInt32(headerBytes, offset, 0);

        AssertFormatError(FileCrypterFormatErrorCode.InvalidArgon2Parameters, headerBytes);
    }

    [Fact]
    public void Parse_WithUnsupportedArgon2Version_ThrowsUnsupportedArgon2Version()
    {
        byte[] headerBytes = CreateValidHeader();
        headerBytes[FileCrypterFormatConstants.Argon2VersionOffset] = 0x12;

        AssertFormatError(FileCrypterFormatErrorCode.UnsupportedArgon2Version, headerBytes);
    }

    [Theory]
    [InlineData(false, FileCrypterFormatConstants.KeyFileHashSha256)]
    [InlineData(true, FileCrypterFormatConstants.KeyFileHashNone)]
    public void Parse_WithInconsistentKeyFileFlagAndHash_ThrowsInvalidKeyFileHashAlgorithm(
        bool keyFileRequired,
        byte keyFileHashAlgorithmId)
    {
        byte[] headerBytes = CreateValidHeader(keyFileRequired);
        headerBytes[FileCrypterFormatConstants.KeyFileHashAlgorithmOffset] = keyFileHashAlgorithmId;

        AssertFormatError(FileCrypterFormatErrorCode.InvalidKeyFileHashAlgorithm, headerBytes);
    }

    [Fact]
    public void Parse_WithNonzeroReservedBytes_ThrowsInvalidReservedBytes()
    {
        byte[] headerBytes = CreateValidHeader();
        headerBytes[FileCrypterFormatConstants.ReservedOffset] = 1;

        AssertFormatError(FileCrypterFormatErrorCode.InvalidReservedBytes, headerBytes);
    }

    private static byte[] CreateValidHeader(bool keyFileRequired = false)
    {
        byte[] headerBytes = new byte[FileCrypterFormatConstants.HeaderLength];

        FileCrypterFormatConstants.Magic.CopyTo(headerBytes);
        WriteUInt16(headerBytes, FileCrypterFormatConstants.VersionOffset, FileCrypterFormatConstants.Version);
        WriteUInt16(headerBytes, FileCrypterFormatConstants.HeaderLengthOffset, FileCrypterFormatConstants.HeaderLength);
        WriteUInt16(
            headerBytes,
            FileCrypterFormatConstants.HeaderFlagsOffset,
            keyFileRequired ? FileCrypterFormatConstants.HeaderFlagKeyFileRequired : (ushort)0);
        headerBytes[FileCrypterFormatConstants.AeadAlgorithmOffset] = FileCrypterFormatConstants.AeadAlgorithmAes256Gcm;
        headerBytes[FileCrypterFormatConstants.KdfAlgorithmOffset] = FileCrypterFormatConstants.KdfAlgorithmArgon2Id;
        headerBytes[FileCrypterFormatConstants.CompressionAlgorithmOffset] = FileCrypterFormatConstants.CompressionNone;
        headerBytes[FileCrypterFormatConstants.PayloadKindOffset] = FileCrypterFormatConstants.PayloadKindSingleFile;
        WriteUInt32(headerBytes, FileCrypterFormatConstants.ChunkSizeOffset, FileCrypterFormatConstants.DefaultChunkSize);
        CreateSalt().CopyTo(headerBytes.AsSpan(FileCrypterFormatConstants.SaltOffset));
        CreateNoncePrefix().CopyTo(headerBytes.AsSpan(FileCrypterFormatConstants.NoncePrefixOffset));
        WriteUInt32(headerBytes, FileCrypterFormatConstants.Argon2MemoryOffset, FileCrypterFormatConstants.DefaultArgon2MemoryKiB);
        WriteUInt32(headerBytes, FileCrypterFormatConstants.Argon2IterationsOffset, FileCrypterFormatConstants.DefaultArgon2Iterations);
        WriteUInt32(headerBytes, FileCrypterFormatConstants.Argon2ParallelismOffset, FileCrypterFormatConstants.DefaultArgon2Parallelism);
        headerBytes[FileCrypterFormatConstants.Argon2VersionOffset] = FileCrypterFormatConstants.Argon2Version;
        headerBytes[FileCrypterFormatConstants.KeyFileHashAlgorithmOffset] = keyFileRequired
            ? FileCrypterFormatConstants.KeyFileHashSha256
            : FileCrypterFormatConstants.KeyFileHashNone;

        return headerBytes;
    }

    private static byte[] CreateSalt()
    {
        return Enumerable.Range(1, FileCrypterFormatConstants.SaltLength)
            .Select(value => (byte)value)
            .ToArray();
    }

    private static byte[] CreateNoncePrefix()
    {
        return Enumerable.Range(101, FileCrypterFormatConstants.NoncePrefixLength)
            .Select(value => (byte)value)
            .ToArray();
    }

    private static void AssertFormatError(FileCrypterFormatErrorCode expectedCode, byte[] headerBytes)
    {
        FileCrypterFormatException exception = Assert.Throws<FileCrypterFormatException>(
            () => FileCrypterHeaderParser.Parse(headerBytes));

        Assert.Equal(expectedCode, exception.Code);
    }

    private static void WriteUInt16(byte[] destination, int offset, ushort value)
    {
        BinaryPrimitives.WriteUInt16LittleEndian(destination.AsSpan(offset, sizeof(ushort)), value);
    }

    private static void WriteUInt32(byte[] destination, int offset, uint value)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.AsSpan(offset, sizeof(uint)), value);
    }
}

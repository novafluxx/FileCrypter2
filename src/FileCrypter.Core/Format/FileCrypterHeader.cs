namespace FileCrypter.Core.Format;

internal sealed class FileCrypterHeader
{
    private readonly byte[] noncePrefix;
    private readonly byte[] salt;

    public FileCrypterHeader(
        ushort formatVersion,
        ushort headerLength,
        ushort headerFlags,
        byte aeadAlgorithmId,
        byte kdfAlgorithmId,
        byte compressionAlgorithmId,
        byte payloadKind,
        uint chunkSize,
        ReadOnlySpan<byte> salt,
        ReadOnlySpan<byte> noncePrefix,
        uint argon2MemoryKiB,
        uint argon2Iterations,
        uint argon2Parallelism,
        byte argon2Version,
        byte keyFileHashAlgorithmId)
    {
        FormatVersion = formatVersion;
        HeaderLength = headerLength;
        HeaderFlags = headerFlags;
        AeadAlgorithmId = aeadAlgorithmId;
        KdfAlgorithmId = kdfAlgorithmId;
        CompressionAlgorithmId = compressionAlgorithmId;
        PayloadKind = payloadKind;
        ChunkSize = chunkSize;
        this.salt = salt.ToArray();
        this.noncePrefix = noncePrefix.ToArray();
        Argon2MemoryKiB = argon2MemoryKiB;
        Argon2Iterations = argon2Iterations;
        Argon2Parallelism = argon2Parallelism;
        Argon2Version = argon2Version;
        KeyFileHashAlgorithmId = keyFileHashAlgorithmId;
    }

    public ushort FormatVersion { get; }

    public ushort HeaderLength { get; }

    public ushort HeaderFlags { get; }

    public byte AeadAlgorithmId { get; }

    public byte KdfAlgorithmId { get; }

    public byte CompressionAlgorithmId { get; }

    public byte PayloadKind { get; }

    public uint ChunkSize { get; }

    public ReadOnlyMemory<byte> Salt => salt;

    public ReadOnlyMemory<byte> NoncePrefix => noncePrefix;

    public uint Argon2MemoryKiB { get; }

    public uint Argon2Iterations { get; }

    public uint Argon2Parallelism { get; }

    public byte Argon2Version { get; }

    public byte KeyFileHashAlgorithmId { get; }

    public bool IsKeyFileRequired => (HeaderFlags & FileCrypterFormatConstants.HeaderFlagKeyFileRequired) != 0;
}

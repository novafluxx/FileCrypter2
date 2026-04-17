using System.Buffers.Binary;
using System.Text;
using FileCrypter.Core.Format;

namespace FileCrypter.Core.Tests;

public sealed class FileCrypterTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task EncryptFileAsyncDecryptFileAsync_RoundTripsWithStagedOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("path api round trip");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);

        string finalEncryptedPath = await FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            CreateFastOptions());
        string finalDecryptedPath = await FileCrypter.DecryptFileAsync(
            encryptedPath,
            decryptedPath,
            Password,
            CreateFastOptions());

        Assert.Equal(encryptedPath, finalEncryptedPath);
        Assert.Equal(decryptedPath, finalDecryptedPath);
        Assert.True(File.Exists(finalEncryptedPath));
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(finalDecryptedPath));
    }

    [Fact]
    public async Task EncryptFileAsync_WhenOutputExistsWithoutOverwrite_AutoRenamesOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string autoRenamedPath = Path.Combine(directory.Path, "plain.txt (1).encrypted");
        byte[] existingBytes = Encoding.UTF8.GetBytes("keep me");
        await File.WriteAllTextAsync(plaintextPath, "new data");
        await File.WriteAllBytesAsync(encryptedPath, existingBytes);

        string finalOutputPath = await FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            CreateFastOptions());

        Assert.Equal(autoRenamedPath, finalOutputPath);
        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(encryptedPath));
        Assert.True(File.Exists(autoRenamedPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task DecryptFileAsync_WhenAuthenticationFails_DoesNotCreateOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        _ = await FileCrypter.EncryptFileAsync(plaintextPath, encryptedPath, Password, CreateFastOptions());

        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptFileAsync(encryptedPath, decryptedPath, "wrong", CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.AuthenticationFailed, exception.Code);
        Assert.False(File.Exists(decryptedPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_WhenCanceledBeforeWrite_DoesNotCreateOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => FileCrypter.EncryptFileAsync(
                plaintextPath,
                encryptedPath,
                Password,
                CreateFastOptions(),
                cancellationToken: cancellation.Token));

        Assert.False(File.Exists(encryptedPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task DecryptFileAsync_WhenCanceledBeforeWrite_DoesNotCreateOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        _ = await FileCrypter.EncryptFileAsync(plaintextPath, encryptedPath, Password, CreateFastOptions());
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => FileCrypter.DecryptFileAsync(
                encryptedPath,
                decryptedPath,
                Password,
                CreateFastOptions(),
                cancellationToken: cancellation.Token));

        Assert.False(File.Exists(decryptedPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_WhenOverwriteIsTrue_ReplacesOutputAfterSuccess()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("replacement");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await File.WriteAllTextAsync(encryptedPath, "old output");

        string finalEncryptedPath = await FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            CreateFastOptions(),
            overwrite: true);
        string finalDecryptedPath = await FileCrypter.DecryptFileAsync(
            encryptedPath,
            decryptedPath,
            Password,
            CreateFastOptions());

        Assert.Equal(encryptedPath, finalEncryptedPath);
        Assert.Equal(decryptedPath, finalDecryptedPath);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(finalDecryptedPath));
    }

    [Fact]
    public async Task EncryptAsyncDecryptAsync_WithSmallPayload_RoundTrips()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("hello from FileCrypter");
        using var plaintext = new MemoryStream(plaintextBytes);
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, CreateFastOptions());

        encrypted.Position = 0;
        await FileCrypter.DecryptAsync(encrypted, decrypted, Password, CreateFastOptions());

        Assert.Equal(plaintextBytes, decrypted.ToArray());
    }

    [Fact]
    public async Task EncryptAsyncDecryptAsync_WithEmptyPayload_RoundTrips()
    {
        using var plaintext = new MemoryStream();
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, CreateFastOptions());

        encrypted.Position = 0;
        await FileCrypter.DecryptAsync(encrypted, decrypted, Password, CreateFastOptions());

        Assert.Empty(decrypted.ToArray());
        Assert.Equal(
            FileCrypterFormatConstants.HeaderLength +
            FileCrypterFormatConstants.ChunkFramePrefixLength +
            FileCrypterFormatConstants.AesGcmTagLength,
            encrypted.Length);
    }

    [Fact]
    public async Task EncryptAsync_WithPayloadEndingOnChunkBoundary_EmitsZeroLengthFinalChunk()
    {
        FileCrypterOptions options = CreateFastOptions();
        byte[] plaintextBytes = Enumerable.Range(0, options.ChunkSize).Select(value => (byte)value).ToArray();
        using var plaintext = new MemoryStream(plaintextBytes);
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, options);

        byte[] encryptedBytes = encrypted.ToArray();
        int finalPrefixOffset =
            FileCrypterFormatConstants.HeaderLength +
            FileCrypterFormatConstants.ChunkFramePrefixLength +
            options.ChunkSize +
            FileCrypterFormatConstants.AesGcmTagLength;
        uint finalPlaintextLength = BinaryPrimitives.ReadUInt32LittleEndian(encryptedBytes.AsSpan(finalPrefixOffset));
        ushort finalFlags = BinaryPrimitives.ReadUInt16LittleEndian(encryptedBytes.AsSpan(finalPrefixOffset + 4));

        Assert.Equal(0u, finalPlaintextLength);
        Assert.Equal(FileCrypterFormatConstants.ChunkFlagFinal, finalFlags);

        encrypted.Position = 0;
        await FileCrypter.DecryptAsync(encrypted, decrypted, Password, options);
        Assert.Equal(plaintextBytes, decrypted.ToArray());
    }

    [Fact]
    public async Task DecryptAsync_WithWrongPassword_ThrowsAuthenticationFailed()
    {
        using var plaintext = new MemoryStream(Encoding.UTF8.GetBytes("do not open"));
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, CreateFastOptions());

        encrypted.Position = 0;
        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(encrypted, decrypted, "wrong password", CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.AuthenticationFailed, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithTamperedCiphertext_ThrowsAuthenticationFailed()
    {
        using var plaintext = new MemoryStream(Encoding.UTF8.GetBytes("tamper check"));
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, CreateFastOptions());
        byte[] encryptedBytes = encrypted.ToArray();
        encryptedBytes[encryptedBytes.Length - FileCrypterFormatConstants.AesGcmTagLength - 1] ^= 0x01;

        using var tampered = new MemoryStream(encryptedBytes);
        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(tampered, decrypted, Password, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.AuthenticationFailed, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithTrailingData_ThrowsTrailingData()
    {
        using var plaintext = new MemoryStream(Encoding.UTF8.GetBytes("trailing data check"));
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, CreateFastOptions());
        byte[] encryptedBytes = encrypted.ToArray();
        byte[] withTrailingData = [.. encryptedBytes, 0xFF];

        using var input = new MemoryStream(withTrailingData);
        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(input, decrypted, Password, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.TrailingData, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithTruncatedChunk_ThrowsTruncatedChunk()
    {
        using var plaintext = new MemoryStream(Encoding.UTF8.GetBytes("truncation check"));
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, CreateFastOptions());
        byte[] encryptedBytes = encrypted.ToArray();
        Array.Resize(ref encryptedBytes, encryptedBytes.Length - 1);

        using var input = new MemoryStream(encryptedBytes);
        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(input, decrypted, Password, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.TruncatedChunk, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithUnknownChunkFlags_ThrowsInvalidChunkFlags()
    {
        using var plaintext = new MemoryStream(Encoding.UTF8.GetBytes("flag check"));
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, CreateFastOptions());
        byte[] encryptedBytes = encrypted.ToArray();
        encryptedBytes[FileCrypterFormatConstants.HeaderLength + 4] = 0x02;

        using var input = new MemoryStream(encryptedBytes);
        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(input, decrypted, Password, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.InvalidChunkFlags, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithNonzeroChunkReservedBytes_ThrowsInvalidChunkReservedBytes()
    {
        using var plaintext = new MemoryStream(Encoding.UTF8.GetBytes("reserved chunk check"));
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, CreateFastOptions());
        byte[] encryptedBytes = encrypted.ToArray();
        encryptedBytes[FileCrypterFormatConstants.HeaderLength + 6] = 0x01;

        using var input = new MemoryStream(encryptedBytes);
        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(input, decrypted, Password, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.InvalidChunkReservedBytes, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithKeyFileRequiredHeader_ThrowsUnsupportedKeyFileRequirement()
    {
        byte[] headerBytes = CreateSupportedHeader();
        BinaryPrimitives.WriteUInt16LittleEndian(
            headerBytes.AsSpan(FileCrypterFormatConstants.HeaderFlagsOffset, sizeof(ushort)),
            FileCrypterFormatConstants.HeaderFlagKeyFileRequired);
        headerBytes[FileCrypterFormatConstants.KeyFileHashAlgorithmOffset] = FileCrypterFormatConstants.KeyFileHashSha256;

        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(new MemoryStream(headerBytes), new MemoryStream(), Password, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.UnsupportedKeyFileRequirement, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithCompressedHeader_ThrowsUnsupportedCompressionAlgorithm()
    {
        byte[] headerBytes = CreateSupportedHeader();
        headerBytes[FileCrypterFormatConstants.CompressionAlgorithmOffset] = FileCrypterFormatConstants.CompressionZstd;

        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(new MemoryStream(headerBytes), new MemoryStream(), Password, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.UnsupportedCompressionAlgorithm, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithArchiveHeader_ThrowsUnsupportedPayloadKind()
    {
        byte[] headerBytes = CreateSupportedHeader();
        headerBytes[FileCrypterFormatConstants.PayloadKindOffset] = FileCrypterFormatConstants.PayloadKindTarArchive;

        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(new MemoryStream(headerBytes), new MemoryStream(), Password, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.UnsupportedPayloadKind, exception.Code);
    }

    [Fact]
    public async Task EncryptAsync_WithInjectedRandomness_MatchesExpectedBytes()
    {
        byte[] randomBytes = Enumerable.Range(0, FileCrypterFormatConstants.SaltLength + FileCrypterFormatConstants.NoncePrefixLength)
            .Select(value => (byte)value)
            .ToArray();
        byte[] expectedEncryptedBytes = Convert.FromHexString(
            "4643525950540D0A0100400000000101000100000100000102030405060708090A0B0C0D0E0F10111213141516170004000001000000010000001300000000000D00000001000000BAED9C9E6CDE84D4A6CCAA222062A01FB62BE218125E3A13BC157843C6");
        using var plaintext = new MemoryStream(Encoding.UTF8.GetBytes("deterministic"));
        using var encrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(
            plaintext,
            encrypted,
            Password,
            CreateFastOptions(new FixedRandomSource(randomBytes)));

        Assert.Equal(expectedEncryptedBytes, encrypted.ToArray());
    }

    private static byte[] CreateSupportedHeader()
    {
        byte[] headerBytes = new byte[FileCrypterFormatConstants.HeaderLength];
        byte[] salt = Enumerable.Range(1, FileCrypterFormatConstants.SaltLength).Select(value => (byte)value).ToArray();
        byte[] noncePrefix = Enumerable.Range(101, FileCrypterFormatConstants.NoncePrefixLength).Select(value => (byte)value).ToArray();
        FileCrypterHeaderWriter.WritePasswordOnly(headerBytes, CreateFastOptions(), salt, noncePrefix);
        return headerBytes;
    }

    private static FileCrypterOptions CreateFastOptions(IFileCrypterRandomSource? randomSource = null)
    {
        return new FileCrypterOptions
        {
            ChunkSize = (int)FileCrypterFormatConstants.MinimumChunkSize,
            Argon2MemoryKiB = 1024,
            Argon2Iterations = 1,
            Argon2Parallelism = 1,
            RandomSource = randomSource,
        };
    }

    private sealed class FixedRandomSource : IFileCrypterRandomSource
    {
        private readonly byte[] bytes;
        private int offset;

        public FixedRandomSource(byte[] bytes)
        {
            this.bytes = bytes;
        }

        public void Fill(Span<byte> destination)
        {
            bytes.AsSpan(offset, destination.Length).CopyTo(destination);
            offset += destination.Length;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "FileCrypter.Tests." + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                Directory.Delete(Path, recursive: true);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }
}

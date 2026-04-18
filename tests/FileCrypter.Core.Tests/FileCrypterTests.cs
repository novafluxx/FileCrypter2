using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using FileCrypter.Core.Cryptography;
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
    public async Task EncryptFileAsyncDecryptFileAsync_WithKeyFile_RoundTripsAndMarksHeader()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("path api key file round trip");
        byte[] keyFileBytes = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await File.WriteAllBytesAsync(keyFilePath, keyFileBytes);

        string finalEncryptedPath = await FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            keyFilePath,
            CreateFastOptions());
        string finalDecryptedPath = await FileCrypter.DecryptFileAsync(
            encryptedPath,
            decryptedPath,
            Password,
            keyFilePath,
            CreateFastOptions());

        byte[] encryptedBytes = await File.ReadAllBytesAsync(finalEncryptedPath);
        FileCrypterHeader header = FileCrypterHeaderParser.Parse(encryptedBytes.AsSpan(0, FileCrypterFormatConstants.HeaderLength));
        Assert.True(header.IsKeyFileRequired);
        Assert.Equal(FileCrypterFormatConstants.KeyFileHashSha256, header.KeyFileHashAlgorithmId);
        Assert.Equal(decryptedPath, finalDecryptedPath);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(finalDecryptedPath));
    }

    [Fact]
    public void GenerateKeyFileBytes_WithInjectedRandomness_ReturnsExpectedBytes()
    {
        byte[] expectedKeyFileBytes = Enumerable.Range(200, FileCrypter.DefaultGeneratedKeyFileSizeBytes)
            .Select(value => (byte)value)
            .ToArray();

        byte[] keyFileBytes = FileCrypter.GenerateKeyFileBytes(
            CreateFastOptions(new FixedRandomSource(expectedKeyFileBytes)));

        Assert.Equal(expectedKeyFileBytes, keyFileBytes);
    }

    [Fact]
    public async Task GenerateKeyFileAsyncEncryptFileAsyncDecryptFileAsync_RoundTrips()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "generated.key");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("generated key file round trip");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);

        string finalKeyFilePath = await FileCrypter.GenerateKeyFileAsync(
            keyFilePath,
            CreateFastOptions());
        string finalEncryptedPath = await FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            finalKeyFilePath,
            CreateFastOptions());
        string finalDecryptedPath = await FileCrypter.DecryptFileAsync(
            finalEncryptedPath,
            decryptedPath,
            Password,
            finalKeyFilePath,
            CreateFastOptions());

        Assert.Equal(keyFilePath, finalKeyFilePath);
        Assert.Equal(FileCrypter.DefaultGeneratedKeyFileSizeBytes, new FileInfo(finalKeyFilePath).Length);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(finalDecryptedPath));
    }

    [Fact]
    public async Task GenerateKeyFileAsync_WhenOutputExistsWithoutOverwrite_AutoRenamesOutput()
    {
        using var directory = new TemporaryDirectory();
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        string autoRenamedPath = Path.Combine(directory.Path, "filecrypter (1).key");
        byte[] existingBytes = Encoding.UTF8.GetBytes("keep me");
        byte[] generatedBytes = Enumerable.Range(1, FileCrypter.DefaultGeneratedKeyFileSizeBytes)
            .Select(value => (byte)value)
            .ToArray();
        await File.WriteAllBytesAsync(keyFilePath, existingBytes);

        string finalKeyFilePath = await FileCrypter.GenerateKeyFileAsync(
            keyFilePath,
            CreateFastOptions(new FixedRandomSource(generatedBytes)));

        Assert.Equal(autoRenamedPath, finalKeyFilePath);
        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(keyFilePath));
        Assert.Equal(generatedBytes, await File.ReadAllBytesAsync(autoRenamedPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task GenerateKeyFileAsync_WhenOutputDirectoryIsMissing_ThrowsAndDoesNotCreateStagingOutput()
    {
        using var directory = new TemporaryDirectory();
        string missingDirectory = Path.Combine(directory.Path, "missing");
        string keyFilePath = Path.Combine(missingDirectory, "filecrypter.key");

        DirectoryNotFoundException exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => FileCrypter.GenerateKeyFileAsync(
                keyFilePath,
                CreateFastOptions()));

        Assert.Contains(missingDirectory, exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(missingDirectory));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public void GenerateKeyFileBytes_WhenByteCountIsTooSmall_Throws()
    {
        ArgumentOutOfRangeException exception = Assert.Throws<ArgumentOutOfRangeException>(
            () => FileCrypter.GenerateKeyFileBytes(
                FileCrypter.DefaultGeneratedKeyFileSizeBytes - 1,
                CreateFastOptions()));

        Assert.Equal("byteCount", exception.ParamName);
    }

    [Fact]
    public async Task GenerateKeyFileAsync_OnUnix_CreatesOutputWithUserOnlyPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");

        string finalKeyFilePath = await FileCrypter.GenerateKeyFileAsync(
            keyFilePath,
            CreateFastOptions());

        const UnixFileMode accessMask =
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupWrite |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherWrite |
            UnixFileMode.OtherExecute;
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(finalKeyFilePath) & accessMask);
    }

    [Fact]
    public async Task EncryptFilesAsyncDecryptFilesAsync_RoundTripsAndCompressesEachFile()
    {
        using var directory = new TemporaryDirectory();
        string inputDirectory = Path.Combine(directory.Path, "input");
        string encryptedDirectory = Path.Combine(directory.Path, "encrypted");
        string decryptedDirectory = Path.Combine(directory.Path, "decrypted");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(encryptedDirectory);
        Directory.CreateDirectory(decryptedDirectory);
        string firstPlaintextPath = Path.Combine(inputDirectory, "first.txt");
        string secondPlaintextPath = Path.Combine(inputDirectory, "second.txt");
        byte[] firstPlaintextBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("first\n", 512)));
        byte[] secondPlaintextBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("second\n", 512)));
        await File.WriteAllBytesAsync(firstPlaintextPath, firstPlaintextBytes);
        await File.WriteAllBytesAsync(secondPlaintextPath, secondPlaintextBytes);

        FileCrypterBatchResult encryptResult = await FileCrypter.EncryptFilesAsync(
            [firstPlaintextPath, secondPlaintextPath],
            encryptedDirectory,
            Password,
            CreateFastOptions());
        FileCrypterBatchResult decryptResult = await FileCrypter.DecryptFilesAsync(
            encryptResult.Items.Select(item => item.OutputPath!),
            decryptedDirectory,
            Password,
            CreateFastOptions());

        string firstEncryptedPath = Path.Combine(encryptedDirectory, "first.txt.encrypted");
        string secondEncryptedPath = Path.Combine(encryptedDirectory, "second.txt.encrypted");
        string firstDecryptedPath = Path.Combine(decryptedDirectory, "first.txt");
        string secondDecryptedPath = Path.Combine(decryptedDirectory, "second.txt");
        byte[] firstEncryptedBytes = await File.ReadAllBytesAsync(firstEncryptedPath);
        FileCrypterHeader firstHeader = FileCrypterHeaderParser.Parse(
            firstEncryptedBytes.AsSpan(0, FileCrypterFormatConstants.HeaderLength));
        Assert.True(encryptResult.Succeeded);
        Assert.Equal(2, encryptResult.SucceededCount);
        Assert.Equal(0, encryptResult.FailedCount);
        Assert.Equal(FileCrypterFormatConstants.CompressionZstd, firstHeader.CompressionAlgorithmId);
        Assert.True(File.Exists(secondEncryptedPath));
        Assert.True(decryptResult.Succeeded);
        Assert.Equal(firstPlaintextBytes, await File.ReadAllBytesAsync(firstDecryptedPath));
        Assert.Equal(secondPlaintextBytes, await File.ReadAllBytesAsync(secondDecryptedPath));
    }

    [Fact]
    public async Task EncryptFilesAsync_WithDuplicateOutputNames_AutoRenamesPerFile()
    {
        using var directory = new TemporaryDirectory();
        string firstDirectory = Path.Combine(directory.Path, "first");
        string secondDirectory = Path.Combine(directory.Path, "second");
        string encryptedDirectory = Path.Combine(directory.Path, "encrypted");
        Directory.CreateDirectory(firstDirectory);
        Directory.CreateDirectory(secondDirectory);
        Directory.CreateDirectory(encryptedDirectory);
        string firstPlaintextPath = Path.Combine(firstDirectory, "same.txt");
        string secondPlaintextPath = Path.Combine(secondDirectory, "same.txt");
        await File.WriteAllTextAsync(firstPlaintextPath, "first");
        await File.WriteAllTextAsync(secondPlaintextPath, "second");

        FileCrypterBatchResult result = await FileCrypter.EncryptFilesAsync(
            [firstPlaintextPath, secondPlaintextPath],
            encryptedDirectory,
            Password,
            CreateFastOptions());

        Assert.True(result.Succeeded);
        Assert.Equal(Path.Combine(encryptedDirectory, "same.txt.encrypted"), result.Items[0].OutputPath);
        Assert.Equal(Path.Combine(encryptedDirectory, "same.txt (1).encrypted"), result.Items[1].OutputPath);
        Assert.True(File.Exists(result.Items[0].OutputPath));
        Assert.True(File.Exists(result.Items[1].OutputPath));
    }

    [Fact]
    public async Task EncryptFilesAsync_WithMixedSuccess_ReturnsPerFileFailureAndContinues()
    {
        using var directory = new TemporaryDirectory();
        string encryptedDirectory = Path.Combine(directory.Path, "encrypted");
        Directory.CreateDirectory(encryptedDirectory);
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string missingPath = Path.Combine(directory.Path, "missing.txt");
        await File.WriteAllTextAsync(plaintextPath, "secret");

        FileCrypterBatchResult result = await FileCrypter.EncryptFilesAsync(
            [plaintextPath, missingPath],
            encryptedDirectory,
            Password,
            CreateFastOptions());

        Assert.False(result.Succeeded);
        Assert.Equal(1, result.SucceededCount);
        Assert.Equal(1, result.FailedCount);
        Assert.True(result.Items[0].Succeeded);
        Assert.False(result.Items[1].Succeeded);
        Assert.IsAssignableFrom<IOException>(result.Items[1].Error);
        Assert.True(File.Exists(Path.Combine(encryptedDirectory, "plain.txt.encrypted")));
    }

    [Fact]
    public async Task EncryptFilesAsync_WithKeyFile_RoundTrips()
    {
        using var directory = new TemporaryDirectory();
        string encryptedDirectory = Path.Combine(directory.Path, "encrypted");
        string decryptedDirectory = Path.Combine(directory.Path, "decrypted");
        Directory.CreateDirectory(encryptedDirectory);
        Directory.CreateDirectory(decryptedDirectory);
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("batch key file");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await File.WriteAllBytesAsync(keyFilePath, Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());

        FileCrypterBatchResult encryptResult = await FileCrypter.EncryptFilesAsync(
            [plaintextPath],
            encryptedDirectory,
            Password,
            keyFilePath,
            CreateFastOptions());
        FileCrypterBatchResult decryptResult = await FileCrypter.DecryptFilesAsync(
            encryptResult.Items.Select(item => item.OutputPath!),
            decryptedDirectory,
            Password,
            keyFilePath,
            CreateFastOptions());

        Assert.True(encryptResult.Succeeded);
        Assert.True(decryptResult.Succeeded);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(Path.Combine(decryptedDirectory, "plain.txt")));
    }

    [Fact]
    public async Task EncryptFilesAsync_WhenFileCountExceedsLimit_Throws()
    {
        using var directory = new TemporaryDirectory();
        string encryptedDirectory = Path.Combine(directory.Path, "encrypted");
        Directory.CreateDirectory(encryptedDirectory);
        string[] inputPaths = Enumerable
            .Range(0, FileCrypter.MaximumBatchFileCount + 1)
            .Select(index => Path.Combine(directory.Path, $"{index}.txt"))
            .ToArray();

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => FileCrypter.EncryptFilesAsync(
                inputPaths,
                encryptedDirectory,
                Password,
                CreateFastOptions()));

        Assert.Contains("supports up to", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EncryptFileAsync_WithOversizedKeyFile_ThrowsAndDoesNotCreateOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "too-large.key");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        await using (FileStream keyFile = File.Create(keyFilePath))
        {
            keyFile.SetLength(FileCrypterFormatConstants.MaximumKeyFileSizeBytes + 1);
        }

        IOException exception = await Assert.ThrowsAsync<IOException>(
            () => FileCrypter.EncryptFileAsync(
                plaintextPath,
                encryptedPath,
                Password,
                keyFilePath,
                CreateFastOptions()));

        Assert.Contains("key file is too large", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(encryptedPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
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
    public async Task EncryptFileAsync_WhenOutputDirectoryIsMissing_ThrowsAndDoesNotCreateStagingOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string missingDirectory = Path.Combine(directory.Path, "missing");
        string encryptedPath = Path.Combine(missingDirectory, "plain.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");

        DirectoryNotFoundException exception = await Assert.ThrowsAsync<DirectoryNotFoundException>(
            () => FileCrypter.EncryptFileAsync(plaintextPath, encryptedPath, Password, CreateFastOptions()));

        Assert.Contains(missingDirectory, exception.Message, StringComparison.Ordinal);
        Assert.False(Directory.Exists(missingDirectory));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_WhenOutputPathIsDirectory_ThrowsAndDoesNotAutoRename()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string autoRenamedPath = Path.Combine(directory.Path, "plain.txt (1).encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        Directory.CreateDirectory(encryptedPath);

        IOException exception = await Assert.ThrowsAsync<IOException>(
            () => FileCrypter.EncryptFileAsync(plaintextPath, encryptedPath, Password, CreateFastOptions()));

        Assert.Contains("directory", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(Directory.Exists(encryptedPath));
        Assert.False(File.Exists(autoRenamedPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_WhenOverwriteIsTrueAndOutputMatchesInput_ThrowsBeforeWriting()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("do not replace me");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => FileCrypter.EncryptFileAsync(
                plaintextPath,
                plaintextPath,
                Password,
                CreateFastOptions(),
                overwrite: true));

        Assert.Equal("outputPath", exception.ParamName);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(plaintextPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_WhenDestinationAppearsBeforeFinalMove_CleansUpStagingOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        byte[] existingBytes = Encoding.UTF8.GetBytes("created by another writer");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        bool createdDestination = false;
        var progress = new CallbackProgress(_ =>
        {
            if (createdDestination)
            {
                return;
            }

            File.WriteAllBytes(encryptedPath, existingBytes);
            createdDestination = true;
        });

        IOException exception = await Assert.ThrowsAsync<IOException>(
            () => FileCrypter.EncryptFileAsync(
                plaintextPath,
                encryptedPath,
                Password,
                CreateFastOptions(progress: progress)));

        Assert.Contains(encryptedPath, exception.Message, StringComparison.Ordinal);
        Assert.True(createdDestination);
        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(encryptedPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_WhenInputPathIsSymbolicLink_ThrowsAndDoesNotCreateOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string inputLinkPath = Path.Combine(directory.Path, "plain-link.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain-link.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        if (!TryCreateFileSymbolicLink(inputLinkPath, plaintextPath))
        {
            return;
        }

        IOException exception = await Assert.ThrowsAsync<IOException>(
            () => FileCrypter.EncryptFileAsync(inputLinkPath, encryptedPath, Password, CreateFastOptions()));

        Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(encryptedPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_WhenOverwriteOutputPathIsSymbolicLink_ThrowsAndPreservesTarget()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string outputTargetPath = Path.Combine(directory.Path, "target.txt");
        string outputLinkPath = Path.Combine(directory.Path, "output-link.encrypted");
        byte[] targetBytes = Encoding.UTF8.GetBytes("existing target");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        await File.WriteAllBytesAsync(outputTargetPath, targetBytes);
        if (!TryCreateFileSymbolicLink(outputLinkPath, outputTargetPath))
        {
            return;
        }

        IOException exception = await Assert.ThrowsAsync<IOException>(
            () => FileCrypter.EncryptFileAsync(
                plaintextPath,
                outputLinkPath,
                Password,
                CreateFastOptions(),
                overwrite: true));

        Assert.Contains("symbolic link", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(targetBytes, await File.ReadAllBytesAsync(outputTargetPath));
        Assert.True(File.Exists(outputLinkPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_WhenOverwriteOutputMatchesInputThroughSymbolicDirectory_ThrowsBeforeWriting()
    {
        using var directory = new TemporaryDirectory();
        string realDirectory = Path.Combine(directory.Path, "real");
        string directoryLink = Path.Combine(directory.Path, "real-link");
        string plaintextPath = Path.Combine(realDirectory, "plain.txt");
        string outputPathThroughLink = Path.Combine(directoryLink, "plain.txt");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("do not replace me through a link");
        Directory.CreateDirectory(realDirectory);
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        if (!TryCreateDirectorySymbolicLink(directoryLink, realDirectory))
        {
            return;
        }

        ArgumentException exception = await Assert.ThrowsAsync<ArgumentException>(
            () => FileCrypter.EncryptFileAsync(
                plaintextPath,
                outputPathThroughLink,
                Password,
                CreateFastOptions(),
                overwrite: true));

        Assert.Equal("outputPath", exception.ParamName);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(plaintextPath));
        Assert.Empty(Directory.GetFiles(realDirectory, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_OnUnix_CreatesOutputWithUserOnlyPermissions()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");

        string finalOutputPath = await FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            CreateFastOptions());

        const UnixFileMode accessMask =
            UnixFileMode.UserRead |
            UnixFileMode.UserWrite |
            UnixFileMode.UserExecute |
            UnixFileMode.GroupRead |
            UnixFileMode.GroupWrite |
            UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead |
            UnixFileMode.OtherWrite |
            UnixFileMode.OtherExecute;
        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(finalOutputPath) & accessMask);
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
    public async Task EncryptAsyncDecryptAsync_WithKeyFile_RoundTrips()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("hello with password and key file");
        byte[] keyFileBytes = Enumerable.Range(32, 64).Select(value => (byte)value).ToArray();
        using var plaintext = new MemoryStream(plaintextBytes);
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, keyFileBytes, CreateFastOptions());

        byte[] encryptedBytes = encrypted.ToArray();
        FileCrypterHeader header = FileCrypterHeaderParser.Parse(encryptedBytes.AsSpan(0, FileCrypterFormatConstants.HeaderLength));
        Assert.True(header.IsKeyFileRequired);

        encrypted.Position = 0;
        await FileCrypter.DecryptAsync(encrypted, decrypted, Password, keyFileBytes, CreateFastOptions());

        Assert.Equal(plaintextBytes, decrypted.ToArray());
    }

    [Fact]
    public async Task DecryptAsync_WithWrongKeyFile_ThrowsAuthenticationFailed()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("key file mismatch check");
        byte[] keyFileBytes = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        byte[] wrongKeyFileBytes = Enumerable.Range(2, 32).Select(value => (byte)value).ToArray();
        using var plaintext = new MemoryStream(plaintextBytes);
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, keyFileBytes, CreateFastOptions());

        encrypted.Position = 0;
        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(encrypted, decrypted, Password, wrongKeyFileBytes, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.AuthenticationFailed, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithExtraKeyFileForPasswordOnlyPayload_IgnoresKeyFile()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("password only still decrypts");
        byte[] keyFileBytes = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        using var plaintext = new MemoryStream(plaintextBytes);
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, CreateFastOptions());

        encrypted.Position = 0;
        await FileCrypter.DecryptAsync(encrypted, decrypted, Password, keyFileBytes, CreateFastOptions());

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
    public async Task EncryptAsyncDecryptAsync_WithCompression_RoundTripsAndMarksHeader()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(new string('A', 200_000));
        FileCrypterOptions options = CreateFastOptions(enableCompression: true);
        using var plaintext = new MemoryStream(plaintextBytes);
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, options);

        byte[] encryptedBytes = encrypted.ToArray();
        FileCrypterHeader header = FileCrypterHeaderParser.Parse(encryptedBytes.AsSpan(0, FileCrypterFormatConstants.HeaderLength));
        Assert.Equal(FileCrypterFormatConstants.CompressionZstd, header.CompressionAlgorithmId);
        Assert.True(encryptedBytes.Length < plaintextBytes.Length);

        encrypted.Position = 0;
        await FileCrypter.DecryptAsync(encrypted, decrypted, Password, CreateFastOptions());

        Assert.Equal(plaintextBytes, decrypted.ToArray());
    }

    [Fact]
    public async Task EncryptAsyncDecryptAsync_WithCompressionAndKeyFile_RoundTrips()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("keyed compressed payload\n", 10_000)));
        byte[] keyFileBytes = Enumerable.Range(1, 64).Select(value => (byte)value).ToArray();
        FileCrypterOptions options = CreateFastOptions(enableCompression: true);
        using var plaintext = new MemoryStream(plaintextBytes);
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(plaintext, encrypted, Password, keyFileBytes, options);

        byte[] encryptedBytes = encrypted.ToArray();
        FileCrypterHeader header = FileCrypterHeaderParser.Parse(encryptedBytes.AsSpan(0, FileCrypterFormatConstants.HeaderLength));
        Assert.True(header.IsKeyFileRequired);
        Assert.Equal(FileCrypterFormatConstants.CompressionZstd, header.CompressionAlgorithmId);

        encrypted.Position = 0;
        await FileCrypter.DecryptAsync(encrypted, decrypted, Password, keyFileBytes, CreateFastOptions());

        Assert.Equal(plaintextBytes, decrypted.ToArray());
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
    public async Task DecryptAsync_WithKeyFileRequiredHeaderAndNoKeyFile_ThrowsKeyFileRequired()
    {
        byte[] headerBytes = CreateSupportedHeader();
        BinaryPrimitives.WriteUInt16LittleEndian(
            headerBytes.AsSpan(FileCrypterFormatConstants.HeaderFlagsOffset, sizeof(ushort)),
            FileCrypterFormatConstants.HeaderFlagKeyFileRequired);
        headerBytes[FileCrypterFormatConstants.KeyFileHashAlgorithmOffset] = FileCrypterFormatConstants.KeyFileHashSha256;

        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(new MemoryStream(headerBytes), new MemoryStream(), Password, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.KeyFileRequired, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithCompressedPayloadAndWrongPassword_ThrowsAuthenticationFailed()
    {
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("compressed secret");
        using var plaintext = new MemoryStream(plaintextBytes);
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(
            plaintext,
            encrypted,
            Password,
            CreateFastOptions(enableCompression: true));

        encrypted.Position = 0;
        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(encrypted, decrypted, "wrong password", CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.AuthenticationFailed, exception.Code);
    }

    [Fact]
    public async Task DecryptAsync_WithMalformedCompressedPayload_ThrowsInvalidCompressedPayload()
    {
        byte[] encryptedBytes = EncryptSingleChunkPayload(
            Encoding.UTF8.GetBytes("this is authenticated, but it is not zstandard"),
            CreateFastOptions(enableCompression: true));

        FileCrypterFormatException exception = await Assert.ThrowsAsync<FileCrypterFormatException>(
            () => FileCrypter.DecryptAsync(new MemoryStream(encryptedBytes), new MemoryStream(), Password, CreateFastOptions()));

        Assert.Equal(FileCrypterFormatErrorCode.InvalidCompressedPayload, exception.Code);
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

    [Theory]
    [MemberData(nameof(CompatibilityVectors))]
    public async Task EncryptAsync_WithCompatibilityVector_MatchesExpectedDigestAndFrames(CompatibilityVector vector)
    {
        byte[] plaintextBytes = CreateVectorPlaintext(vector.Name);
        byte[] randomBytes = Enumerable
            .Range(vector.RandomStart, FileCrypterFormatConstants.SaltLength + FileCrypterFormatConstants.NoncePrefixLength)
            .Select(value => (byte)value)
            .ToArray();
        using var plaintext = new MemoryStream(plaintextBytes);
        using var encrypted = new MemoryStream();
        using var decrypted = new MemoryStream();

        await FileCrypter.EncryptAsync(
            plaintext,
            encrypted,
            Password,
            CreateFastOptions(new FixedRandomSource(randomBytes)));

        byte[] encryptedBytes = encrypted.ToArray();
        IReadOnlyList<ChunkVector> chunks = ReadChunkVectors(encryptedBytes);

        Assert.Equal(vector.ExpectedPlaintextLength, plaintextBytes.Length);
        Assert.Equal(vector.ExpectedEncryptedLength, encryptedBytes.Length);
        Assert.Equal(vector.ExpectedEncryptedSha256Hex, Convert.ToHexString(SHA256.HashData(encryptedBytes)));
        Assert.Equal(vector.ExpectedHeaderHex, Convert.ToHexString(encryptedBytes.AsSpan(0, FileCrypterFormatConstants.HeaderLength)));
        Assert.Equal(vector.ExpectedChunks.Count, chunks.Count);
        for (int index = 0; index < vector.ExpectedChunks.Count; index++)
        {
            Assert.Equal(vector.ExpectedChunks[index], chunks[index]);
        }

        encrypted.Position = 0;
        await FileCrypter.DecryptAsync(encrypted, decrypted, Password, CreateFastOptions());
        Assert.Equal(plaintextBytes, decrypted.ToArray());
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

    private static byte[] EncryptSingleChunkPayload(byte[] payload, FileCrypterOptions options)
    {
        byte[] headerBytes = new byte[FileCrypterFormatConstants.HeaderLength];
        byte[] salt = Enumerable.Range(1, FileCrypterFormatConstants.SaltLength).Select(value => (byte)value).ToArray();
        byte[] noncePrefix = Enumerable.Range(101, FileCrypterFormatConstants.NoncePrefixLength).Select(value => (byte)value).ToArray();
        FileCrypterHeaderWriter.WritePasswordOnly(headerBytes, options, salt, noncePrefix);
        FileCrypterHeader header = FileCrypterHeaderParser.Parse(headerBytes);
        byte[] key = FileCrypterKeyDeriver.DerivePasswordOnlyKey(Password, header);

        try
        {
            byte[] prefix = new byte[FileCrypterFormatConstants.ChunkFramePrefixLength];
            BinaryPrimitives.WriteUInt32LittleEndian(prefix.AsSpan(0, sizeof(uint)), (uint)payload.Length);
            BinaryPrimitives.WriteUInt16LittleEndian(
                prefix.AsSpan(4, sizeof(ushort)),
                FileCrypterFormatConstants.ChunkFlagFinal);

            byte[] nonce = new byte[FileCrypterFormatConstants.AesGcmNonceLength];
            header.NoncePrefix.CopyTo(nonce);
            byte[] associatedData = new byte[
                FileCrypterFormatConstants.HeaderLength + FileCrypterFormatConstants.ChunkFramePrefixLength];
            headerBytes.CopyTo(associatedData, 0);
            prefix.CopyTo(associatedData, FileCrypterFormatConstants.HeaderLength);
            byte[] ciphertext = new byte[payload.Length];
            byte[] tag = new byte[FileCrypterFormatConstants.AesGcmTagLength];

            using var aesGcm = new AesGcm(key, FileCrypterFormatConstants.AesGcmTagLength);
            aesGcm.Encrypt(nonce, payload, ciphertext, tag, associatedData);

            using var encrypted = new MemoryStream();
            encrypted.Write(headerBytes);
            encrypted.Write(prefix);
            encrypted.Write(ciphertext);
            encrypted.Write(tag);
            return encrypted.ToArray();
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static IEnumerable<object[]> CompatibilityVectors()
    {
        const string zeroStartHeader =
            "4643525950540D0A0100400000000101000100000100000102030405060708090A0B0C0D0E0F1011121314151617000400000100000001000000130000000000";

        yield return
        [
            new CompatibilityVector(
                Name: "empty",
                ExpectedPlaintextLength: 0,
                RandomStart: 0,
                ExpectedEncryptedLength: 88,
                ExpectedEncryptedSha256Hex: "EBFBAC483FFB75A805D456718D1C680119CA127ED289153FF1E5004935795084",
                ExpectedHeaderHex: zeroStartHeader,
                ExpectedChunks:
                [
                    new ChunkVector(
                        PrefixHex: "0000000001000000",
                        TagHex: "5005EAF8395E79184BB3EFC2482D76F3"),
                ]),
        ];

        yield return
        [
            new CompatibilityVector(
                Name: "small-text",
                ExpectedPlaintextLength: 13,
                RandomStart: 0,
                ExpectedEncryptedLength: 101,
                ExpectedEncryptedSha256Hex: "236C3BCFE0BC19917619E64396C64310D4E381233FC4DC890A072A7A4B42B9AA",
                ExpectedHeaderHex: zeroStartHeader,
                ExpectedChunks:
                [
                    new ChunkVector(
                        PrefixHex: "0D00000001000000",
                        TagHex: "62A01FB62BE218125E3A13BC157843C6"),
                ]),
        ];

        yield return
        [
            new CompatibilityVector(
                Name: "chunk-boundary",
                ExpectedPlaintextLength: (int)FileCrypterFormatConstants.MinimumChunkSize,
                RandomStart: 24,
                ExpectedEncryptedLength: 65648,
                ExpectedEncryptedSha256Hex: "126F2EB854EEDA5BC3F599FC1F4A504E6B0F05B584C5155C51334875970A9DA9",
                ExpectedHeaderHex:
                    "4643525950540D0A010040000000010100010000010018191A1B1C1D1E1F202122232425262728292A2B2C2D2E2F000400000100000001000000130000000000",
                ExpectedChunks:
                [
                    new ChunkVector(
                        PrefixHex: "0000010000000000",
                        TagHex: "CB60E5DC2A51F5762477B7373CE669A9"),
                    new ChunkVector(
                        PrefixHex: "0000000001000000",
                        TagHex: "E935FB59E33E5AB1157DA6A004BF03F1"),
                ]),
        ];

        yield return
        [
            new CompatibilityVector(
                Name: "multi-chunk",
                ExpectedPlaintextLength: (int)FileCrypterFormatConstants.MinimumChunkSize + 17,
                RandomStart: 48,
                ExpectedEncryptedLength: 65665,
                ExpectedEncryptedSha256Hex: "E3E3FAC63488BF0585F427875DAA0D8A02582A1F56C0B501C2CFD3B9FFDE4B5B",
                ExpectedHeaderHex:
                    "4643525950540D0A0100400000000101000100000100303132333435363738393A3B3C3D3E3F4041424344454647000400000100000001000000130000000000",
                ExpectedChunks:
                [
                    new ChunkVector(
                        PrefixHex: "0000010000000000",
                        TagHex: "1006D8D39F494D822E815878CC82E855"),
                    new ChunkVector(
                        PrefixHex: "1100000001000000",
                        TagHex: "DFC4382DC1018277617AB5B18C99D67F"),
                ]),
        ];
    }

    private static byte[] CreateVectorPlaintext(string name)
    {
        return name switch
        {
            "empty" => [],
            "small-text" => Encoding.UTF8.GetBytes("deterministic"),
            "chunk-boundary" => Enumerable
                .Range(0, (int)FileCrypterFormatConstants.MinimumChunkSize)
                .Select(value => (byte)value)
                .ToArray(),
            "multi-chunk" => Enumerable
                .Range(0, (int)FileCrypterFormatConstants.MinimumChunkSize + 17)
                .Select(value => (byte)(255 - value))
                .ToArray(),
            _ => throw new ArgumentOutOfRangeException(nameof(name), name, "Unknown compatibility vector."),
        };
    }

    private static IReadOnlyList<ChunkVector> ReadChunkVectors(byte[] encryptedBytes)
    {
        var chunks = new List<ChunkVector>();
        int offset = FileCrypterFormatConstants.HeaderLength;

        while (offset < encryptedBytes.Length)
        {
            string prefixHex = Convert.ToHexString(
                encryptedBytes.AsSpan(offset, FileCrypterFormatConstants.ChunkFramePrefixLength));
            uint plaintextLength = BinaryPrimitives.ReadUInt32LittleEndian(encryptedBytes.AsSpan(offset, sizeof(uint)));
            offset += FileCrypterFormatConstants.ChunkFramePrefixLength + checked((int)plaintextLength);
            string tagHex = Convert.ToHexString(
                encryptedBytes.AsSpan(offset, FileCrypterFormatConstants.AesGcmTagLength));
            offset += FileCrypterFormatConstants.AesGcmTagLength;
            chunks.Add(new ChunkVector(prefixHex, tagHex));
        }

        return chunks;
    }

    private static FileCrypterOptions CreateFastOptions(
        IFileCrypterRandomSource? randomSource = null,
        IProgress<FileCrypterProgress>? progress = null,
        bool enableCompression = false)
    {
        return new FileCrypterOptions
        {
            ChunkSize = (int)FileCrypterFormatConstants.MinimumChunkSize,
            Argon2MemoryKiB = 1024,
            Argon2Iterations = 1,
            Argon2Parallelism = 1,
            EnableCompression = enableCompression,
            RandomSource = randomSource,
            Progress = progress,
        };
    }

    private static bool TryCreateFileSymbolicLink(string linkPath, string targetPath)
    {
        try
        {
            File.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private static bool TryCreateDirectorySymbolicLink(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
            return true;
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            return false;
        }
    }

    private sealed class CallbackProgress : IProgress<FileCrypterProgress>
    {
        private readonly Action<FileCrypterProgress> callback;

        public CallbackProgress(Action<FileCrypterProgress> callback)
        {
            this.callback = callback;
        }

        public void Report(FileCrypterProgress value)
        {
            callback(value);
        }
    }

    public sealed record CompatibilityVector(
        string Name,
        int ExpectedPlaintextLength,
        int RandomStart,
        int ExpectedEncryptedLength,
        string ExpectedEncryptedSha256Hex,
        string ExpectedHeaderHex,
        IReadOnlyList<ChunkVector> ExpectedChunks);

    public sealed record ChunkVector(string PrefixHex, string TagHex);

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

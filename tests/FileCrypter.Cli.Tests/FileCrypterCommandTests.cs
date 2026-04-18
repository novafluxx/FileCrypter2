using System.Text;
using FileCrypter.Core;

public sealed class FileCrypterCommandTests
{
    private const string Password = "correct horse battery staple";
    private const int CompressionAlgorithmOffset = 16;
    private const int PayloadKindOffset = 17;
    private const byte CompressionZstd = 1;
    private const byte PayloadKindTarArchive = 2;

    [Fact]
    public async Task EncryptAndDecrypt_WithExplicitOutputs_Succeeds()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("cli round trip");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        var encryptConsole = TestConsole.CreateRedirected();
        var decryptConsole = TestConsole.CreateRedirected();

        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password]);
        int decryptExitCode = await CreateCommand(decryptConsole).RunAsync(
            ["decrypt", encryptedPath, decryptedPath, "--password", Password]);

        Assert.Equal(0, encryptExitCode);
        Assert.Equal(0, decryptExitCode);
        Assert.Equal(Path.GetFullPath(encryptedPath) + Environment.NewLine, encryptConsole.Output);
        Assert.Equal(Path.GetFullPath(decryptedPath) + Environment.NewLine, decryptConsole.Output);
        Assert.Contains($"Encrypting: 100% ({plaintextBytes.Length}/{plaintextBytes.Length} bytes)", encryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("Decrypting: 100% (", decryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptedPath));
    }

    [Fact]
    public async Task EncryptAndDecrypt_WithKeyFile_Succeeds()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("cli key file round trip");
        byte[] keyFileBytes = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await File.WriteAllBytesAsync(keyFilePath, keyFileBytes);
        var encryptConsole = TestConsole.CreateRedirected();
        var decryptConsole = TestConsole.CreateRedirected();

        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password, "--key-file", keyFilePath]);
        int decryptExitCode = await CreateCommand(decryptConsole).RunAsync(
            ["decrypt", encryptedPath, decryptedPath, "--password", Password, "--key-file", keyFilePath]);

        Assert.Equal(0, encryptExitCode);
        Assert.Equal(0, decryptExitCode);
        Assert.Equal(Path.GetFullPath(encryptedPath) + Environment.NewLine, encryptConsole.Output);
        Assert.Equal(Path.GetFullPath(decryptedPath) + Environment.NewLine, decryptConsole.Output);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptedPath));
    }

    [Fact]
    public async Task EncryptAndDecrypt_WithGeneratedKeyFile_Succeeds()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "generated.key");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("cli generated key file round trip");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        var encryptConsole = TestConsole.CreateRedirected();
        var decryptConsole = TestConsole.CreateRedirected();

        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password, "--generate-key-file", keyFilePath]);
        int decryptExitCode = await CreateCommand(decryptConsole).RunAsync(
            ["decrypt", encryptedPath, decryptedPath, "--password", Password, "--key-file", keyFilePath]);

        Assert.Equal(0, encryptExitCode);
        Assert.Equal(0, decryptExitCode);
        Assert.Equal(FileCrypter.Core.FileCrypter.DefaultGeneratedKeyFileSizeBytes, new FileInfo(keyFilePath).Length);
        Assert.Equal(Path.GetFullPath(encryptedPath) + Environment.NewLine, encryptConsole.Output);
        Assert.Contains($"Generated key file: {Path.GetFullPath(keyFilePath)}", encryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("losing it makes the encrypted file unrecoverable", encryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptedPath));
    }

    [Fact]
    public async Task Encrypt_WithGeneratedKeyFileExistingWithoutOverwrite_AutoRenamesKeyFileAndSucceeds()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        string generatedKeyFilePath = Path.Combine(directory.Path, "filecrypter (1).key");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("cli generated key file auto rename");
        byte[] existingKeyBytes = Encoding.UTF8.GetBytes("existing key file");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await File.WriteAllBytesAsync(keyFilePath, existingKeyBytes);
        var encryptConsole = TestConsole.CreateRedirected();

        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password, "--generate-key-file", keyFilePath]);
        await FileCrypter.Core.FileCrypter.DecryptFileAsync(
            encryptedPath,
            decryptedPath,
            Password,
            generatedKeyFilePath,
            CreateFastOptions());

        Assert.Equal(0, encryptExitCode);
        Assert.Equal(existingKeyBytes, await File.ReadAllBytesAsync(keyFilePath));
        Assert.Equal(FileCrypter.Core.FileCrypter.DefaultGeneratedKeyFileSizeBytes, new FileInfo(generatedKeyFilePath).Length);
        Assert.Contains($"Generated key file: {Path.GetFullPath(generatedKeyFilePath)}", encryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptedPath));
    }

    [Fact]
    public async Task EncryptAndDecrypt_WithCompression_SucceedsAndReportsProgress()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("compress me\n", 512)));
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        var encryptConsole = TestConsole.CreateRedirected();
        var decryptConsole = TestConsole.CreateRedirected();

        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password, "--compress"]);
        int decryptExitCode = await CreateCommand(decryptConsole).RunAsync(
            ["decrypt", encryptedPath, decryptedPath, "--password", Password]);

        byte[] encryptedBytes = await File.ReadAllBytesAsync(encryptedPath);
        Assert.Equal(0, encryptExitCode);
        Assert.Equal(0, decryptExitCode);
        Assert.Equal(CompressionZstd, encryptedBytes[CompressionAlgorithmOffset]);
        Assert.Equal(Path.GetFullPath(encryptedPath) + Environment.NewLine, encryptConsole.Output);
        Assert.Equal(Path.GetFullPath(decryptedPath) + Environment.NewLine, decryptConsole.Output);
        Assert.Contains($"Encrypting: 100% ({plaintextBytes.Length}/{plaintextBytes.Length} bytes)", encryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("Decrypting: 100% (", decryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptedPath));
    }

    [Fact]
    public async Task EncryptAndDecrypt_WithCompressionAndKeyFile_Succeeds()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("keyed compression\n", 512)));
        byte[] keyFileBytes = Enumerable.Range(1, 32).Select(value => (byte)value).ToArray();
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await File.WriteAllBytesAsync(keyFilePath, keyFileBytes);
        var encryptConsole = TestConsole.CreateRedirected();
        var decryptConsole = TestConsole.CreateRedirected();

        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password, "--key-file", keyFilePath, "--compress"]);
        int decryptExitCode = await CreateCommand(decryptConsole).RunAsync(
            ["decrypt", encryptedPath, decryptedPath, "--password", Password, "--key-file", keyFilePath]);

        byte[] encryptedBytes = await File.ReadAllBytesAsync(encryptedPath);
        Assert.Equal(0, encryptExitCode);
        Assert.Equal(0, decryptExitCode);
        Assert.Equal(CompressionZstd, encryptedBytes[CompressionAlgorithmOffset]);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptedPath));
    }

    [Fact]
    public async Task BatchEncryptAndBatchDecrypt_SucceedsAndCompresses()
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
        string firstEncryptedPath = Path.Combine(encryptedDirectory, "first.txt.encrypted");
        string secondEncryptedPath = Path.Combine(encryptedDirectory, "second.txt.encrypted");
        string firstDecryptedPath = Path.Combine(decryptedDirectory, "first.txt");
        string secondDecryptedPath = Path.Combine(decryptedDirectory, "second.txt");
        var encryptConsole = TestConsole.CreateRedirected();
        var decryptConsole = TestConsole.CreateRedirected();

        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["batch-encrypt", encryptedDirectory, firstPlaintextPath, secondPlaintextPath, "--password", Password]);
        int decryptExitCode = await CreateCommand(decryptConsole).RunAsync(
            ["batch-decrypt", decryptedDirectory, firstEncryptedPath, secondEncryptedPath, "--password", Password]);

        byte[] firstEncryptedBytes = await File.ReadAllBytesAsync(firstEncryptedPath);
        Assert.Equal(0, encryptExitCode);
        Assert.Equal(0, decryptExitCode);
        Assert.Equal(
            Path.GetFullPath(firstEncryptedPath) + Environment.NewLine +
            Path.GetFullPath(secondEncryptedPath) + Environment.NewLine,
            encryptConsole.Output);
        Assert.Equal(CompressionZstd, firstEncryptedBytes[CompressionAlgorithmOffset]);
        Assert.Contains("Batch complete: 2/2 succeeded.", encryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("Batch complete: 2/2 succeeded.", decryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.Equal(firstPlaintextBytes, await File.ReadAllBytesAsync(firstDecryptedPath));
        Assert.Equal(secondPlaintextBytes, await File.ReadAllBytesAsync(secondDecryptedPath));
    }

    [Fact]
    public async Task BatchEncryptAndBatchDecrypt_WithKeyFile_Succeeds()
    {
        using var directory = new TemporaryDirectory();
        string encryptedDirectory = Path.Combine(directory.Path, "encrypted");
        string decryptedDirectory = Path.Combine(directory.Path, "decrypted");
        Directory.CreateDirectory(encryptedDirectory);
        Directory.CreateDirectory(decryptedDirectory);
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        string encryptedPath = Path.Combine(encryptedDirectory, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(decryptedDirectory, "plain.txt");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("batch key file");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await File.WriteAllBytesAsync(keyFilePath, Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var encryptConsole = TestConsole.CreateRedirected();
        var decryptConsole = TestConsole.CreateRedirected();

        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["batch-encrypt", encryptedDirectory, plaintextPath, "--password", Password, "--key-file", keyFilePath]);
        int decryptExitCode = await CreateCommand(decryptConsole).RunAsync(
            ["batch-decrypt", decryptedDirectory, encryptedPath, "--password", Password, "--key-file", keyFilePath]);

        Assert.Equal(0, encryptExitCode);
        Assert.Equal(0, decryptExitCode);
        Assert.Equal(Path.GetFullPath(encryptedPath) + Environment.NewLine, encryptConsole.Output);
        Assert.Equal(Path.GetFullPath(decryptedPath) + Environment.NewLine, decryptConsole.Output);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptedPath));
    }

    [Fact]
    public async Task ArchiveEncryptAndArchiveDecrypt_Succeeds()
    {
        using var directory = new TemporaryDirectory();
        string inputDirectory = Path.Combine(directory.Path, "input");
        string outputDirectory = Path.Combine(directory.Path, "output");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(outputDirectory);
        string firstPlaintextPath = Path.Combine(inputDirectory, "first.txt");
        string secondPlaintextPath = Path.Combine(inputDirectory, "second.txt");
        string encryptedArchivePath = Path.Combine(directory.Path, "bundle.tar.zst.encrypted");
        string firstOutputPath = Path.Combine(outputDirectory, "first.txt");
        string secondOutputPath = Path.Combine(outputDirectory, "second.txt");
        byte[] firstPlaintextBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("first\n", 512)));
        byte[] secondPlaintextBytes = Encoding.UTF8.GetBytes("second");
        await File.WriteAllBytesAsync(firstPlaintextPath, firstPlaintextBytes);
        await File.WriteAllBytesAsync(secondPlaintextPath, secondPlaintextBytes);
        var encryptConsole = TestConsole.CreateRedirected();
        var decryptConsole = TestConsole.CreateRedirected();

        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["archive-encrypt", encryptedArchivePath, firstPlaintextPath, secondPlaintextPath, "--password", Password]);
        int decryptExitCode = await CreateCommand(decryptConsole).RunAsync(
            ["archive-decrypt", encryptedArchivePath, outputDirectory, "--password", Password]);

        byte[] encryptedBytes = await File.ReadAllBytesAsync(encryptedArchivePath);
        Assert.Equal(0, encryptExitCode);
        Assert.Equal(0, decryptExitCode);
        Assert.Equal(CompressionZstd, encryptedBytes[CompressionAlgorithmOffset]);
        Assert.Equal(PayloadKindTarArchive, encryptedBytes[PayloadKindOffset]);
        Assert.Equal(Path.GetFullPath(encryptedArchivePath) + Environment.NewLine, encryptConsole.Output);
        Assert.Equal(
            Path.GetFullPath(firstOutputPath) + Environment.NewLine +
            Path.GetFullPath(secondOutputPath) + Environment.NewLine,
            decryptConsole.Output);
        Assert.Equal(firstPlaintextBytes, await File.ReadAllBytesAsync(firstOutputPath));
        Assert.Equal(secondPlaintextBytes, await File.ReadAllBytesAsync(secondOutputPath));
    }

    [Fact]
    public async Task ArchiveEncryptAndArchiveDecrypt_WithKeyFile_Succeeds()
    {
        using var directory = new TemporaryDirectory();
        string outputDirectory = Path.Combine(directory.Path, "output");
        Directory.CreateDirectory(outputDirectory);
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        string encryptedArchivePath = Path.Combine(directory.Path, "bundle.tar.zst.encrypted");
        string extractedPath = Path.Combine(outputDirectory, "plain.txt");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("archive cli key file");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await File.WriteAllBytesAsync(keyFilePath, Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var encryptConsole = TestConsole.CreateRedirected();
        var decryptConsole = TestConsole.CreateRedirected();

        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["archive-encrypt", encryptedArchivePath, plaintextPath, "--password", Password, "--key-file", keyFilePath]);
        int decryptExitCode = await CreateCommand(decryptConsole).RunAsync(
            ["archive-decrypt", encryptedArchivePath, outputDirectory, "--password", Password, "--key-file", keyFilePath]);

        Assert.Equal(0, encryptExitCode);
        Assert.Equal(0, decryptExitCode);
        Assert.Equal(Path.GetFullPath(encryptedArchivePath) + Environment.NewLine, encryptConsole.Output);
        Assert.Equal(Path.GetFullPath(extractedPath) + Environment.NewLine, decryptConsole.Output);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(extractedPath));
    }

    [Fact]
    public async Task BatchEncrypt_WithMixedSuccess_ContinuesAndReturnsFailure()
    {
        using var directory = new TemporaryDirectory();
        string encryptedDirectory = Path.Combine(directory.Path, "encrypted");
        Directory.CreateDirectory(encryptedDirectory);
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string missingPath = Path.Combine(directory.Path, "missing.txt");
        string encryptedPath = Path.Combine(encryptedDirectory, "plain.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(
            ["batch-encrypt", encryptedDirectory, plaintextPath, missingPath, "--password", Password]);

        Assert.Equal(1, exitCode);
        Assert.Equal(Path.GetFullPath(encryptedPath) + Environment.NewLine, console.Output);
        Assert.Contains($"Failed: {missingPath}", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("Batch complete: 1/2 succeeded.", console.ErrorOutput, StringComparison.Ordinal);
        Assert.True(File.Exists(encryptedPath));
    }

    [Fact]
    public async Task Decrypt_WithCompressedPayloadAndWrongPassword_FailsSafely()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("do not open\n", 512)));
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        var encryptConsole = TestConsole.CreateRedirected();
        var decryptConsole = TestConsole.CreateRedirected();
        int encryptExitCode = await CreateCommand(encryptConsole).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password, "--compress"]);

        int decryptExitCode = await CreateCommand(decryptConsole).RunAsync(
            ["decrypt", encryptedPath, decryptedPath, "--password", "wrong"]);

        Assert.Equal(0, encryptExitCode);
        Assert.Equal(1, decryptExitCode);
        Assert.Contains("AuthenticationFailed", decryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("Check the password and key file, then try again.", decryptConsole.ErrorOutput, StringComparison.Ordinal);
        Assert.False(File.Exists(decryptedPath));
    }

    [Fact]
    public async Task Encrypt_WhenPasswordIsMissingAndInputIsRedirected_Fails()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["encrypt", plaintextPath]);

        Assert.Equal(1, exitCode);
        Assert.Contains("A password is required.", console.ErrorOutput, StringComparison.Ordinal);
        Assert.False(File.Exists(plaintextPath + ".encrypted"));
    }

    [Fact]
    public async Task Help_IncludesKeyFileRecoveryAndSizeWarnings()
    {
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["--help"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("--compress", console.Output, StringComparison.Ordinal);
        Assert.Contains("Decryption detects compressed files automatically", console.Output, StringComparison.Ordinal);
        Assert.Contains("lost or changed key files cannot be recovered", console.Output, StringComparison.Ordinal);
        Assert.Contains("Existing key files may be up to 16 MiB", console.Output, StringComparison.Ordinal);
        Assert.Contains("--generate-key-file", console.Output, StringComparison.Ordinal);
        Assert.Contains("create a new 32-byte key file", console.Output, StringComparison.Ordinal);
        Assert.Contains("batch-encrypt", console.Output, StringComparison.Ordinal);
        Assert.Contains("Batch encryption compresses each file automatically", console.Output, StringComparison.Ordinal);
        Assert.Contains("archive-encrypt", console.Output, StringComparison.Ordinal);
        Assert.Contains("Archive encryption writes one compressed tar archive payload", console.Output, StringComparison.Ordinal);
        Assert.Contains("settings set compression-default", console.Output, StringComparison.Ordinal);
        Assert.Empty(console.ErrorOutput);
    }

    [Fact]
    public async Task SettingsShow_WhenSettingsFileIsMissing_ReportsCompressionDefaultOff()
    {
        using var directory = new TemporaryDirectory();
        string settingsPath = Path.Combine(directory.Path, "settings.json");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console, settingsStore: CreateSettingsStore(settingsPath))
            .RunAsync(["settings", "show"]);

        Assert.Equal(0, exitCode);
        Assert.Contains("Compression default: off", console.Output, StringComparison.Ordinal);
        Assert.Contains($"Settings file: {settingsPath}", console.Output, StringComparison.Ordinal);
        Assert.False(File.Exists(settingsPath));
        Assert.Empty(console.ErrorOutput);
    }

    [Fact]
    public async Task SettingsSetCompressionDefault_WritesSettingsFile()
    {
        using var directory = new TemporaryDirectory();
        string settingsPath = Path.Combine(directory.Path, "settings.json");
        FileCrypterSettingsStore settingsStore = CreateSettingsStore(settingsPath);
        var setConsole = TestConsole.CreateRedirected();
        var showConsole = TestConsole.CreateRedirected();

        int setExitCode = await CreateCommand(setConsole, settingsStore: settingsStore)
            .RunAsync(["settings", "set", "compression-default", "on"]);
        int showExitCode = await CreateCommand(showConsole, settingsStore: settingsStore)
            .RunAsync(["settings", "show"]);

        Assert.Equal(0, setExitCode);
        Assert.Equal(0, showExitCode);
        Assert.True(File.Exists(settingsPath));
        Assert.Contains("Compression default: on", setConsole.Output, StringComparison.Ordinal);
        Assert.Contains("Compression default: on", showConsole.Output, StringComparison.Ordinal);
        Assert.Empty(setConsole.ErrorOutput);
        Assert.Empty(showConsole.ErrorOutput);
    }

    [Fact]
    public async Task Encrypt_WhenCompressionDefaultIsOn_CompressesWithoutCompressOption()
    {
        using var directory = new TemporaryDirectory();
        string settingsPath = Path.Combine(directory.Path, "settings.json");
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes(string.Concat(Enumerable.Repeat("default compression\n", 512)));
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        FileCrypterSettingsStore settingsStore = CreateSettingsStore(settingsPath);
        await settingsStore.SaveAsync(new FileCrypterSettings { EnableCompressionByDefault = true });
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console, settingsStore: settingsStore)
            .RunAsync(["encrypt", plaintextPath, encryptedPath, "--password", Password]);

        byte[] encryptedBytes = await File.ReadAllBytesAsync(encryptedPath);
        Assert.Equal(0, exitCode);
        Assert.Equal(CompressionZstd, encryptedBytes[CompressionAlgorithmOffset]);
        Assert.Equal(Path.GetFullPath(encryptedPath) + Environment.NewLine, console.Output);
    }

    [Fact]
    public async Task Encrypt_WhenCompressionDefaultIsOffAndCompressIsOmitted_DoesNotCompress()
    {
        using var directory = new TemporaryDirectory();
        string settingsPath = Path.Combine(directory.Path, "settings.json");
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "no default compression");
        FileCrypterSettingsStore settingsStore = CreateSettingsStore(settingsPath);
        await settingsStore.SaveAsync(new FileCrypterSettings { EnableCompressionByDefault = false });
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console, settingsStore: settingsStore)
            .RunAsync(["encrypt", plaintextPath, encryptedPath, "--password", Password]);

        byte[] encryptedBytes = await File.ReadAllBytesAsync(encryptedPath);
        Assert.Equal(0, exitCode);
        Assert.Equal(0, encryptedBytes[CompressionAlgorithmOffset]);
    }

    [Fact]
    public async Task Encrypt_WhenSettingsFileIsInvalid_FailsWithSettingsTroubleshooting()
    {
        using var directory = new TemporaryDirectory();
        string settingsPath = Path.Combine(directory.Path, "settings.json");
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        await File.WriteAllTextAsync(settingsPath, "{ invalid json");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console, settingsStore: CreateSettingsStore(settingsPath))
            .RunAsync(["encrypt", plaintextPath, encryptedPath, "--password", Password]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Settings error:", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("settings set compression-default", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Empty(console.Output);
        Assert.False(File.Exists(encryptedPath));
    }

    [Fact]
    public async Task Decrypt_WhenSettingsFileIsInvalid_DoesNotReadSettings()
    {
        using var directory = new TemporaryDirectory();
        string settingsPath = Path.Combine(directory.Path, "settings.json");
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("decrypt ignores settings");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await FileCrypter.Core.FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            CreateFastOptions());
        await File.WriteAllTextAsync(settingsPath, "{ invalid json");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console, settingsStore: CreateSettingsStore(settingsPath))
            .RunAsync(["decrypt", encryptedPath, decryptedPath, "--password", Password]);

        Assert.Equal(0, exitCode);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptedPath));
        Assert.Equal(Path.GetFullPath(decryptedPath) + Environment.NewLine, console.Output);
        Assert.DoesNotContain("Settings error:", console.ErrorOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encrypt_WhenInputFileIsMissing_Fails()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "missing.txt");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["encrypt", plaintextPath, "--password", Password]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Input file does not exist:", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Empty(console.Output);
    }

    [Fact]
    public async Task Encrypt_WhenOutputIsOmitted_AppendsEncryptedSuffix()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string expectedEncryptedPath = plaintextPath + ".encrypted";
        await File.WriteAllTextAsync(plaintextPath, "secret");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["encrypt", plaintextPath, "--password", Password]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(expectedEncryptedPath));
        Assert.Equal(Path.GetFullPath(expectedEncryptedPath) + Environment.NewLine, console.Output);
    }

    [Fact]
    public async Task Decrypt_WhenOutputIsOmitted_RemovesEncryptedSuffix()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = plaintextPath + ".encrypted";
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("secret");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await FileCrypter.Core.FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            CreateFastOptions());
        File.Delete(plaintextPath);
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["decrypt", encryptedPath, "--password", Password]);

        Assert.Equal(0, exitCode);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(plaintextPath));
        Assert.Equal(Path.GetFullPath(plaintextPath) + Environment.NewLine, console.Output);
    }

    [Fact]
    public async Task Decrypt_ReportsProgressToErrorWithoutChangingOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = plaintextPath + ".encrypted";
        string decryptedPath = plaintextPath + ".decrypted";
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("secret");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await FileCrypter.Core.FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            CreateFastOptions());
        long encryptedLength = new FileInfo(encryptedPath).Length;
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["decrypt", encryptedPath, decryptedPath, "--password", Password]);

        Assert.Equal(0, exitCode);
        Assert.Equal(Path.GetFullPath(decryptedPath) + Environment.NewLine, console.Output);
        Assert.Contains($"Decrypting: 100% ({encryptedLength}/{encryptedLength} bytes)", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptedPath));
    }

    [Fact]
    public async Task Encrypt_WhenProgressCallbackIsSupplied_ReportsToCallbackAndError()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = plaintextPath + ".encrypted";
        await File.WriteAllTextAsync(plaintextPath, "secret");
        var progressReports = new List<FileCrypterProgress>();
        var options = CreateFastOptions(new CallbackProgress(progressReports.Add));
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console, options).RunAsync(["encrypt", plaintextPath, encryptedPath, "--password", Password]);

        Assert.Equal(0, exitCode);
        Assert.NotEmpty(progressReports);
        Assert.Contains("Encrypting: 100%", console.ErrorOutput, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encrypt_WhenOutputExistsWithoutOverwrite_AutoRenamesOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string expectedRenamedPath = Path.Combine(directory.Path, "plain.txt (1).encrypted");
        byte[] existingBytes = Encoding.UTF8.GetBytes("keep me");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        await File.WriteAllBytesAsync(encryptedPath, existingBytes);
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["encrypt", plaintextPath, encryptedPath, "--password", Password]);

        Assert.Equal(0, exitCode);
        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(encryptedPath));
        Assert.True(File.Exists(expectedRenamedPath));
        Assert.Equal(Path.GetFullPath(expectedRenamedPath) + Environment.NewLine, console.Output);
    }

    [Fact]
    public async Task Encrypt_WhenOverwriteIsSpecified_ReplacesOutput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("replacement");
        byte[] existingBytes = Encoding.UTF8.GetBytes("old output");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        await File.WriteAllBytesAsync(encryptedPath, existingBytes);
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password, "--overwrite"]);
        await FileCrypter.Core.FileCrypter.DecryptFileAsync(
            encryptedPath,
            decryptedPath,
            Password,
            CreateFastOptions());

        Assert.Equal(0, exitCode);
        Assert.NotEqual(existingBytes, await File.ReadAllBytesAsync(encryptedPath));
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptedPath));
        Assert.Equal(Path.GetFullPath(encryptedPath) + Environment.NewLine, console.Output);
    }

    [Fact]
    public async Task Decrypt_WithWrongPassword_FailsSafely()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        await FileCrypter.Core.FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            CreateFastOptions());
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["decrypt", encryptedPath, decryptedPath, "--password", "wrong"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("AuthenticationFailed", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("Check the password and key file, then try again.", console.ErrorOutput, StringComparison.Ordinal);
        Assert.False(File.Exists(decryptedPath));
    }

    [Fact]
    public async Task Decrypt_WhenKeyFileIsRequiredButMissing_FailsWithHint()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        await File.WriteAllBytesAsync(keyFilePath, Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        await FileCrypter.Core.FileCrypter.EncryptFileAsync(
            plaintextPath,
            encryptedPath,
            Password,
            keyFilePath,
            CreateFastOptions());
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["decrypt", encryptedPath, decryptedPath, "--password", Password]);

        Assert.Equal(1, exitCode);
        Assert.Contains("KeyFileRequired", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("Provide the matching key file with --key-file.", console.ErrorOutput, StringComparison.Ordinal);
        Assert.False(File.Exists(decryptedPath));
    }

    [Fact]
    public async Task Decrypt_WithMalformedInput_FailsWithTroubleshooting()
    {
        using var directory = new TemporaryDirectory();
        string encryptedPath = Path.Combine(directory.Path, "not-filecrypter.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "out.txt");
        await File.WriteAllBytesAsync(encryptedPath, Enumerable.Range(0, 64).Select(value => (byte)value).ToArray());
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["decrypt", encryptedPath, decryptedPath, "--password", Password]);

        Assert.Equal(1, exitCode);
        Assert.Contains("InvalidMagic", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("Choose a FileCrypter .encrypted file", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Empty(console.Output);
        Assert.False(File.Exists(decryptedPath));
    }

    [Fact]
    public async Task Encrypt_WhenOutputDirectoryIsMissing_FailsWithPathTroubleshooting()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "missing", "plain.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(["encrypt", plaintextPath, encryptedPath, "--password", Password]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Path error:", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("output directory already exists", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Empty(console.Output);
        Assert.False(File.Exists(encryptedPath));
    }

    [Fact]
    public async Task Encrypt_WhenKeyFileIsTooLarge_FailsWithPathTroubleshooting()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "too-large.key");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        await using (FileStream keyFile = File.Create(keyFilePath))
        {
            keyFile.SetLength((16L * 1_024L * 1_024L) + 1);
        }

        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password, "--key-file", keyFilePath]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Path error:", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("key file is too large", console.ErrorOutput, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(console.Output);
        Assert.False(File.Exists(encryptedPath));
    }

    [Fact]
    public async Task Encrypt_WhenGeneratedKeyFileDirectoryIsMissing_FailsWithPathTroubleshooting()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string missingDirectory = Path.Combine(directory.Path, "missing");
        string keyFilePath = Path.Combine(missingDirectory, "generated.key");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password, "--generate-key-file", keyFilePath]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Path error:", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("output directory already exists", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Empty(console.Output);
        Assert.False(File.Exists(encryptedPath));
        Assert.False(Directory.Exists(missingDirectory));
    }

    [Fact]
    public async Task Encrypt_WhenKeyFileAndGenerateKeyFileAreBothSupplied_Fails()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        string generatedKeyFilePath = Path.Combine(directory.Path, "generated.key");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        await File.WriteAllBytesAsync(keyFilePath, Enumerable.Range(1, 32).Select(value => (byte)value).ToArray());
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(
            [
                "encrypt",
                plaintextPath,
                "--password",
                Password,
                "--key-file",
                keyFilePath,
                "--generate-key-file",
                generatedKeyFilePath,
            ]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Use either --key-file or --generate-key-file, not both.", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Empty(console.Output);
        Assert.False(File.Exists(generatedKeyFilePath));
    }

    [Fact]
    public async Task Encrypt_WhenGeneratedKeyFileMatchesInput_FailsWithoutChangingInput()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("keep the input");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(
            [
                "encrypt",
                plaintextPath,
                encryptedPath,
                "--password",
                Password,
                "--generate-key-file",
                plaintextPath,
                "--overwrite",
            ]);

        Assert.Equal(1, exitCode);
        Assert.Contains("input and generated key file paths must be different", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Empty(console.Output);
        Assert.False(File.Exists(encryptedPath));
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(plaintextPath));
    }

    [Fact]
    public async Task Encrypt_WhenGeneratedKeyFileMatchesOutput_FailsBeforeWriting()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        await File.WriteAllTextAsync(plaintextPath, "secret");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(
            ["encrypt", plaintextPath, encryptedPath, "--password", Password, "--generate-key-file", encryptedPath]);

        Assert.Equal(1, exitCode);
        Assert.Contains("output and generated key file paths must be different", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Empty(console.Output);
        Assert.False(File.Exists(encryptedPath));
    }

    [Fact]
    public async Task Decrypt_WhenGenerateKeyFileIsSupplied_Fails()
    {
        using var directory = new TemporaryDirectory();
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string generatedKeyFilePath = Path.Combine(directory.Path, "generated.key");
        await File.WriteAllTextAsync(encryptedPath, "not checked");
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(
            ["decrypt", encryptedPath, "--password", Password, "--generate-key-file", generatedKeyFilePath]);

        Assert.Equal(1, exitCode);
        Assert.Contains("--generate-key-file is only supported with encrypt.", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Empty(console.Output);
        Assert.False(File.Exists(generatedKeyFilePath));
    }

    [Fact]
    public async Task Encrypt_WhenOverwriteOutputMatchesInput_FailsWithPathTroubleshooting()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("keep the input");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);
        var console = TestConsole.CreateRedirected();

        int exitCode = await CreateCommand(console).RunAsync(
            ["encrypt", plaintextPath, plaintextPath, "--password", Password, "--overwrite"]);

        Assert.Equal(1, exitCode);
        Assert.Contains("Path error:", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Contains("input and output paths must be different", console.ErrorOutput, StringComparison.Ordinal);
        Assert.Empty(console.Output);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(plaintextPath));
    }

    [Fact]
    public async Task Encrypt_WhenPasswordIsPrompted_DoesNotEchoPassword()
    {
        using var directory = new TemporaryDirectory();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = plaintextPath + ".encrypted";
        await File.WriteAllTextAsync(plaintextPath, "secret");
        var console = TestConsole.CreateInteractive(Password);

        int exitCode = await CreateCommand(console).RunAsync(["encrypt", plaintextPath]);

        Assert.Equal(0, exitCode);
        Assert.True(File.Exists(encryptedPath));
        Assert.Contains("Password:", console.ErrorOutput, StringComparison.Ordinal);
        Assert.DoesNotContain(Password, console.ErrorOutput, StringComparison.Ordinal);
    }

    private static FileCrypterCommand CreateCommand(
        TestConsole console,
        FileCrypterOptions? options = null,
        FileCrypterSettingsStore? settingsStore = null)
    {
        return new FileCrypterCommand(console, options ?? CreateFastOptions(), settingsStore);
    }

    private static FileCrypterSettingsStore CreateSettingsStore(string settingsPath)
    {
        return new FileCrypterSettingsStore(settingsPath);
    }

    private static FileCrypterOptions CreateFastOptions(IProgress<FileCrypterProgress>? progress = null)
    {
        return new FileCrypterOptions
        {
            ChunkSize = 65_536,
            Argon2MemoryKiB = 1024,
            Argon2Iterations = 1,
            Argon2Parallelism = 1,
            Progress = progress,
        };
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

    private sealed class TestConsole : IFileCrypterConsole
    {
        private readonly Queue<ConsoleKeyInfo> keyPresses;
        private readonly StringWriter output = new();
        private readonly StringWriter error = new();

        private TestConsole(bool isInputRedirected, string stdin, IEnumerable<ConsoleKeyInfo> keyPresses)
        {
            IsInputRedirected = isInputRedirected;
            In = new StringReader(stdin);
            this.keyPresses = new Queue<ConsoleKeyInfo>(keyPresses);
        }

        public TextReader In { get; }

        public TextWriter Out => output;

        public TextWriter Error => error;

        public bool IsInputRedirected { get; }

        public string Output => output.ToString();

        public string ErrorOutput => error.ToString();

        public static TestConsole CreateRedirected(string stdin = "")
        {
            return new TestConsole(isInputRedirected: true, stdin, []);
        }

        public static TestConsole CreateInteractive(string password)
        {
            IEnumerable<ConsoleKeyInfo> keyPresses = password
                .Select(character => new ConsoleKeyInfo(character, 0, shift: false, alt: false, control: false))
                .Append(new ConsoleKeyInfo('\r', ConsoleKey.Enter, shift: false, alt: false, control: false));

            return new TestConsole(isInputRedirected: false, stdin: "", keyPresses);
        }

        public ConsoleKeyInfo ReadKey(bool intercept)
        {
            Assert.True(intercept);
            return keyPresses.Dequeue();
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "FileCrypter.Cli.Tests." + Guid.NewGuid().ToString("N"));
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

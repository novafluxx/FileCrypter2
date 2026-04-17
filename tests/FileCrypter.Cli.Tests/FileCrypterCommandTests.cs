using System.Text;
using FileCrypter.Core;

public sealed class FileCrypterCommandTests
{
    private const string Password = "correct horse battery staple";

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

    private static FileCrypterCommand CreateCommand(TestConsole console, FileCrypterOptions? options = null)
    {
        return new FileCrypterCommand(console, options ?? CreateFastOptions());
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

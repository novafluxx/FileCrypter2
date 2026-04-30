using System.Text;
using FileCrypter.Desktop.Services;
using FileCrypter.Core;

namespace FileCrypter.Desktop.Tests.Services;

public sealed class FileCrypterWorkflowServiceTests
{
    private const string Password = "correct horse battery staple";

    [Fact]
    public async Task EncryptFileAsyncDecryptFileAsync_WithDefaultPaths_RoundTripsAndReportsProgress()
    {
        using var directory = new TemporaryDirectory();
        var service = new FileCrypterWorkflowService();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = plaintextPath + ".encrypted";
        string decryptedPath = plaintextPath;
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("workflow service file round trip");
        var encryptProgressReports = new List<FileCrypterProgress>();
        var decryptProgressReports = new List<FileCrypterProgress>();
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);

        EncryptFileResult encryptResult = await service.EncryptFileAsync(
            new EncryptFileRequest(
                plaintextPath,
                OutputPath: null,
                Password,
                EnableCompression: false,
                NeverOverwriteExistingFiles: false,
                KeyFilePath: null,
                GenerateKeyFilePath: null),
            new CallbackProgress(encryptProgressReports.Add),
            CancellationToken.None);
        File.Delete(plaintextPath);
        DecryptFileResult decryptResult = await service.DecryptFileAsync(
            new DecryptFileRequest(
                encryptResult.OutputPath,
                OutputPath: null,
                Password,
                NeverOverwriteExistingFiles: false,
                KeyFilePath: null),
            new CallbackProgress(decryptProgressReports.Add),
            CancellationToken.None);

        Assert.Equal(encryptedPath, encryptResult.OutputPath);
        Assert.Equal(decryptedPath, decryptResult.OutputPath);
        Assert.True(File.Exists(encryptResult.OutputPath));
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptResult.OutputPath));
        Assert.Contains(encryptProgressReports, report => report.InputBytes == plaintextBytes.Length);
        Assert.Contains(decryptProgressReports, report => report.InputBytes == new FileInfo(encryptResult.OutputPath).Length);
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_WithGeneratedKeyFile_RoundTripsThroughDecrypt()
    {
        using var directory = new TemporaryDirectory();
        var service = new FileCrypterWorkflowService();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string keyFilePath = Path.Combine(directory.Path, "filecrypter.key");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string decryptedPath = Path.Combine(directory.Path, "plain.txt.decrypted");
        byte[] plaintextBytes = Encoding.UTF8.GetBytes("workflow generated key file");
        await File.WriteAllBytesAsync(plaintextPath, plaintextBytes);

        EncryptFileResult encryptResult = await service.EncryptFileAsync(
            new EncryptFileRequest(
                plaintextPath,
                encryptedPath,
                Password,
                EnableCompression: false,
                NeverOverwriteExistingFiles: false,
                KeyFilePath: null,
                GenerateKeyFilePath: keyFilePath),
            progress: null,
            CancellationToken.None);
        DecryptFileResult decryptResult = await service.DecryptFileAsync(
            new DecryptFileRequest(
                encryptResult.OutputPath,
                decryptedPath,
                Password,
                NeverOverwriteExistingFiles: false,
                encryptResult.GeneratedKeyFilePath),
            progress: null,
            CancellationToken.None);

        Assert.Equal(keyFilePath, encryptResult.GeneratedKeyFilePath);
        Assert.Equal(encryptedPath, encryptResult.OutputPath);
        Assert.Equal(decryptedPath, decryptResult.OutputPath);
        Assert.True(File.Exists(keyFilePath));
        Assert.Equal(FileCrypter.Core.FileCrypter.DefaultGeneratedKeyFileSizeBytes, new FileInfo(keyFilePath).Length);
        Assert.Equal(plaintextBytes, await File.ReadAllBytesAsync(decryptResult.OutputPath));
    }

    [Fact]
    public async Task EncryptFilesAsyncDecryptFilesAsync_RoundTripsAndAggregatesProgress()
    {
        using var directory = new TemporaryDirectory();
        var service = new FileCrypterWorkflowService();
        string inputDirectory = Path.Combine(directory.Path, "input");
        string encryptedDirectory = Path.Combine(directory.Path, "encrypted");
        string decryptedDirectory = Path.Combine(directory.Path, "decrypted");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(encryptedDirectory);
        Directory.CreateDirectory(decryptedDirectory);
        string firstPlaintextPath = Path.Combine(inputDirectory, "first.txt");
        string secondPlaintextPath = Path.Combine(inputDirectory, "second.txt");
        byte[] firstPlaintextBytes = Encoding.UTF8.GetBytes("first workflow batch file");
        byte[] secondPlaintextBytes = Encoding.UTF8.GetBytes("second workflow batch file");
        var encryptProgressReports = new List<BatchOperationProgress>();
        var decryptProgressReports = new List<BatchOperationProgress>();
        await File.WriteAllBytesAsync(firstPlaintextPath, firstPlaintextBytes);
        await File.WriteAllBytesAsync(secondPlaintextPath, secondPlaintextBytes);

        BatchTransformResult encryptResult = await service.EncryptFilesAsync(
            new BatchTransformRequest(
                [firstPlaintextPath, secondPlaintextPath],
                encryptedDirectory,
                Password,
                NeverOverwriteExistingFiles: false,
                KeyFilePath: null),
            new BatchCallbackProgress(encryptProgressReports.Add),
            CancellationToken.None);
        BatchTransformResult decryptResult = await service.DecryptFilesAsync(
            new BatchTransformRequest(
                encryptResult.Items.Select(item => item.OutputPath!).ToArray(),
                decryptedDirectory,
                Password,
                NeverOverwriteExistingFiles: false,
                KeyFilePath: null),
            new BatchCallbackProgress(decryptProgressReports.Add),
            CancellationToken.None);

        Assert.True(encryptResult.Succeeded);
        Assert.True(decryptResult.Succeeded);
        Assert.Equal(2, encryptResult.SucceededCount);
        Assert.Equal(Path.Combine(encryptedDirectory, "first.txt.encrypted"), encryptResult.Items[0].OutputPath);
        Assert.Equal(Path.Combine(encryptedDirectory, "second.txt.encrypted"), encryptResult.Items[1].OutputPath);
        Assert.Equal(firstPlaintextBytes, await File.ReadAllBytesAsync(Path.Combine(decryptedDirectory, "first.txt")));
        Assert.Equal(secondPlaintextBytes, await File.ReadAllBytesAsync(Path.Combine(decryptedDirectory, "second.txt")));
        Assert.Contains(encryptProgressReports, report => report.CompletedFiles == 0 && report.TotalFiles == 2);
        Assert.Equal(100, encryptProgressReports[^1].Percent);
        Assert.Equal(100, decryptProgressReports[^1].Percent);
    }

    [Fact]
    public async Task EncryptArchiveAsyncDecryptArchiveAsync_RoundTripsSelectedFiles()
    {
        using var directory = new TemporaryDirectory();
        var service = new FileCrypterWorkflowService();
        string inputDirectory = Path.Combine(directory.Path, "input");
        string encryptedDirectory = Path.Combine(directory.Path, "encrypted");
        string extractedDirectory = Path.Combine(directory.Path, "extracted");
        Directory.CreateDirectory(inputDirectory);
        Directory.CreateDirectory(encryptedDirectory);
        Directory.CreateDirectory(extractedDirectory);
        string firstPlaintextPath = Path.Combine(inputDirectory, "first.txt");
        string secondPlaintextPath = Path.Combine(inputDirectory, "second.txt");
        byte[] firstPlaintextBytes = Encoding.UTF8.GetBytes("first workflow archive file");
        byte[] secondPlaintextBytes = Encoding.UTF8.GetBytes("second workflow archive file");
        await File.WriteAllBytesAsync(firstPlaintextPath, firstPlaintextBytes);
        await File.WriteAllBytesAsync(secondPlaintextPath, secondPlaintextBytes);

        ArchiveEncryptResult encryptResult = await service.EncryptArchiveAsync(
            new ArchiveEncryptRequest(
                [firstPlaintextPath, secondPlaintextPath],
                encryptedDirectory,
                Password,
                NeverOverwriteExistingFiles: false,
                KeyFilePath: null,
                ArchiveName: "bundle.tar.zst.encrypted"),
            progress: null,
            CancellationToken.None);
        ArchiveDecryptResult decryptResult = await service.DecryptArchiveAsync(
            new ArchiveDecryptRequest(
                encryptResult.OutputPath,
                extractedDirectory,
                Password,
                NeverOverwriteExistingFiles: false,
                KeyFilePath: null),
            progress: null,
            CancellationToken.None);

        Assert.Equal(Path.Combine(encryptedDirectory, "bundle.tar.zst.encrypted"), encryptResult.OutputPath);
        Assert.Equal(
            [Path.Combine(extractedDirectory, "first.txt"), Path.Combine(extractedDirectory, "second.txt")],
            decryptResult.OutputPaths);
        Assert.Equal(firstPlaintextBytes, await File.ReadAllBytesAsync(Path.Combine(extractedDirectory, "first.txt")));
        Assert.Equal(secondPlaintextBytes, await File.ReadAllBytesAsync(Path.Combine(extractedDirectory, "second.txt")));
        Assert.Empty(Directory.GetFiles(encryptedDirectory, "*.tmp"));
        Assert.Empty(Directory.GetFiles(extractedDirectory, "*.tmp"));
    }

    [Fact]
    public async Task EncryptFileAsync_WhenNeverOverwriteExistingFilesIsTrue_AutoRenamesOutput()
    {
        using var directory = new TemporaryDirectory();
        var service = new FileCrypterWorkflowService();
        string plaintextPath = Path.Combine(directory.Path, "plain.txt");
        string encryptedPath = Path.Combine(directory.Path, "plain.txt.encrypted");
        string autoRenamedPath = Path.Combine(directory.Path, "plain.txt (1).encrypted");
        byte[] existingBytes = Encoding.UTF8.GetBytes("keep me");
        await File.WriteAllTextAsync(plaintextPath, "new data");
        await File.WriteAllBytesAsync(encryptedPath, existingBytes);

        EncryptFileResult result = await service.EncryptFileAsync(
            new EncryptFileRequest(
                plaintextPath,
                encryptedPath,
                Password,
                EnableCompression: false,
                NeverOverwriteExistingFiles: true,
                KeyFilePath: null,
                GenerateKeyFilePath: null),
            progress: null,
            CancellationToken.None);

        Assert.Equal(autoRenamedPath, result.OutputPath);
        Assert.Equal(existingBytes, await File.ReadAllBytesAsync(encryptedPath));
        Assert.True(File.Exists(autoRenamedPath));
        Assert.Empty(Directory.GetFiles(directory.Path, "*.tmp"));
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

    private sealed class BatchCallbackProgress : IProgress<BatchOperationProgress>
    {
        private readonly Action<BatchOperationProgress> callback;

        public BatchCallbackProgress(Action<BatchOperationProgress> callback)
        {
            this.callback = callback;
        }

        public void Report(BatchOperationProgress value)
        {
            callback(value);
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "FileCrypter.Desktop.Tests." + Guid.NewGuid().ToString("N"));
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

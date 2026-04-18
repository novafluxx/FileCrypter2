using FileCrypter.Core;
using CoreFileCrypter = FileCrypter.Core.FileCrypter;

namespace FileCrypter.App.Services;

public sealed class FileCrypterWorkflowService : IFileCrypterWorkflowService
{
    private const string DefaultEncryptedSuffix = ".encrypted";
    private const string DefaultDecryptedSuffix = ".decrypted";

    public async Task<EncryptFileResult> EncryptFileAsync(
        EncryptFileRequest request,
        IProgress<FileCrypterProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        string outputPath = string.IsNullOrWhiteSpace(request.OutputPath)
            ? request.SourcePath + DefaultEncryptedSuffix
            : request.OutputPath;
        bool overwrite = !request.NeverOverwriteExistingFiles;
        string? keyFilePath = string.IsNullOrWhiteSpace(request.KeyFilePath) ? null : request.KeyFilePath;
        string? generatedKeyFilePath = null;

        FileCrypterOptions options = new()
        {
            EnableCompression = request.EnableCompression,
            Progress = progress,
        };

        if (!string.IsNullOrWhiteSpace(request.GenerateKeyFilePath))
        {
            generatedKeyFilePath = await CoreFileCrypter.GenerateKeyFileAsync(
                request.GenerateKeyFilePath,
                options,
                overwrite,
                cancellationToken).ConfigureAwait(false);
            keyFilePath = generatedKeyFilePath;
        }

        string finalOutputPath = keyFilePath is null
            ? await CoreFileCrypter.EncryptFileAsync(
                request.SourcePath,
                outputPath,
                request.Password,
                options,
                overwrite,
                cancellationToken).ConfigureAwait(false)
            : await CoreFileCrypter.EncryptFileAsync(
                request.SourcePath,
                outputPath,
                request.Password,
                keyFilePath,
                options,
                overwrite,
                cancellationToken).ConfigureAwait(false);

        return new EncryptFileResult(finalOutputPath, generatedKeyFilePath);
    }

    public async Task<DecryptFileResult> DecryptFileAsync(
        DecryptFileRequest request,
        IProgress<FileCrypterProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        string outputPath = string.IsNullOrWhiteSpace(request.OutputPath)
            ? GetDefaultDecryptedOutputPath(request.SourcePath)
            : request.OutputPath;
        bool overwrite = !request.NeverOverwriteExistingFiles;
        string? keyFilePath = string.IsNullOrWhiteSpace(request.KeyFilePath) ? null : request.KeyFilePath;

        FileCrypterOptions options = new()
        {
            Progress = progress,
        };

        string finalOutputPath = keyFilePath is null
            ? await CoreFileCrypter.DecryptFileAsync(
                request.SourcePath,
                outputPath,
                request.Password,
                options,
                overwrite,
                cancellationToken).ConfigureAwait(false)
            : await CoreFileCrypter.DecryptFileAsync(
                request.SourcePath,
                outputPath,
                request.Password,
                keyFilePath,
                options,
                overwrite,
                cancellationToken).ConfigureAwait(false);

        return new DecryptFileResult(finalOutputPath);
    }

    private static string GetDefaultDecryptedOutputPath(string sourcePath)
    {
        string fileName = Path.GetFileName(sourcePath);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("The source path must include a file name.", nameof(sourcePath));
        }

        string outputFileName = fileName.EndsWith(DefaultEncryptedSuffix, StringComparison.OrdinalIgnoreCase)
            ? fileName[..^DefaultEncryptedSuffix.Length]
            : fileName + DefaultDecryptedSuffix;
        string? directoryPath = Path.GetDirectoryName(sourcePath);

        return string.IsNullOrWhiteSpace(directoryPath)
            ? outputFileName
            : Path.Combine(directoryPath, outputFileName);
    }
}

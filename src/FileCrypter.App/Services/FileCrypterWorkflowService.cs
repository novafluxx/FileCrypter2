using FileCrypter.Core;
using CoreFileCrypter = FileCrypter.Core.FileCrypter;

namespace FileCrypter.App.Services;

public sealed class FileCrypterWorkflowService : IFileCrypterWorkflowService
{
    private const string DefaultEncryptedSuffix = ".encrypted";

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
}

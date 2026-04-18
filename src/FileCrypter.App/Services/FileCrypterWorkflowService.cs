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

    public Task<BatchTransformResult> EncryptFilesAsync(
        BatchTransformRequest request,
        IProgress<BatchOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        return TransformFilesAsync(request, progress, encrypt: true, cancellationToken);
    }

    public async Task<ArchiveEncryptResult> EncryptArchiveAsync(
        ArchiveEncryptRequest request,
        IProgress<FileCrypterProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);
        if (request.SourcePaths.Count == 0)
        {
            throw new ArgumentException("Choose at least one input file.", nameof(request));
        }

        if (!ArchiveFileNameHelper.TryCreateArchiveFileName(
            request.ArchiveName,
            out string archiveFileName,
            out string? archiveNameError))
        {
            throw new ArgumentException(archiveNameError ?? "Archive name is invalid.", nameof(request));
        }

        string outputPath = Path.Combine(request.OutputDirectory, archiveFileName);
        bool overwrite = !request.NeverOverwriteExistingFiles;
        string? keyFilePath = string.IsNullOrWhiteSpace(request.KeyFilePath) ? null : request.KeyFilePath;
        FileCrypterOptions options = new()
        {
            Progress = progress,
        };

        string finalOutputPath = keyFilePath is null
            ? await CoreFileCrypter.EncryptArchiveAsync(
                request.SourcePaths,
                outputPath,
                request.Password,
                options,
                overwrite,
                cancellationToken).ConfigureAwait(false)
            : await CoreFileCrypter.EncryptArchiveAsync(
                request.SourcePaths,
                outputPath,
                request.Password,
                keyFilePath,
                options,
                overwrite,
                cancellationToken).ConfigureAwait(false);

        return new ArchiveEncryptResult(finalOutputPath);
    }

    public async Task<ArchiveDecryptResult> DecryptArchiveAsync(
        ArchiveDecryptRequest request,
        IProgress<FileCrypterProgress>? progress,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);

        bool overwrite = !request.NeverOverwriteExistingFiles;
        string? keyFilePath = string.IsNullOrWhiteSpace(request.KeyFilePath) ? null : request.KeyFilePath;
        FileCrypterOptions options = new()
        {
            Progress = progress,
        };

        IReadOnlyList<string> outputPaths = keyFilePath is null
            ? await CoreFileCrypter.DecryptArchiveAsync(
                request.SourcePath,
                request.OutputDirectory,
                request.Password,
                options,
                overwrite,
                cancellationToken).ConfigureAwait(false)
            : await CoreFileCrypter.DecryptArchiveAsync(
                request.SourcePath,
                request.OutputDirectory,
                request.Password,
                keyFilePath,
                options,
                overwrite,
                cancellationToken).ConfigureAwait(false);

        return new ArchiveDecryptResult(outputPaths);
    }

    public Task<BatchTransformResult> DecryptFilesAsync(
        BatchTransformRequest request,
        IProgress<BatchOperationProgress>? progress,
        CancellationToken cancellationToken)
    {
        return TransformFilesAsync(request, progress, encrypt: false, cancellationToken);
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

    private static async Task<BatchTransformResult> TransformFilesAsync(
        BatchTransformRequest request,
        IProgress<BatchOperationProgress>? progress,
        bool encrypt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.SourcePaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OutputDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Password);
        if (request.SourcePaths.Count == 0)
        {
            throw new ArgumentException("Choose at least one input file.", nameof(request));
        }

        string? keyFilePath = string.IsNullOrWhiteSpace(request.KeyFilePath) ? null : request.KeyFilePath;
        bool overwrite = !request.NeverOverwriteExistingFiles;
        var progressAggregator = new BatchProgressAggregator(request.SourcePaths, progress);
        progressAggregator.ReportStarted();

        FileCrypterOptions options = new()
        {
            Progress = progressAggregator,
        };

        FileCrypterBatchResult result = encrypt
            ? keyFilePath is null
                ? await CoreFileCrypter.EncryptFilesAsync(
                    request.SourcePaths,
                    request.OutputDirectory,
                    request.Password,
                    options,
                    overwrite,
                    cancellationToken).ConfigureAwait(false)
                : await CoreFileCrypter.EncryptFilesAsync(
                    request.SourcePaths,
                    request.OutputDirectory,
                    request.Password,
                    keyFilePath,
                    options,
                    overwrite,
                    cancellationToken).ConfigureAwait(false)
            : keyFilePath is null
                ? await CoreFileCrypter.DecryptFilesAsync(
                    request.SourcePaths,
                    request.OutputDirectory,
                    request.Password,
                    options,
                    overwrite,
                    cancellationToken).ConfigureAwait(false)
                : await CoreFileCrypter.DecryptFilesAsync(
                    request.SourcePaths,
                    request.OutputDirectory,
                    request.Password,
                    keyFilePath,
                    options,
                    overwrite,
                    cancellationToken).ConfigureAwait(false);

        progressAggregator.ReportCompleted();
        return new BatchTransformResult(
            result.Items
                .Select(item => new BatchTransformItemResult(
                    item.InputPath,
                    item.RequestedOutputPath,
                    item.OutputPath,
                    item.Error))
                .ToArray());
    }

    private sealed class BatchProgressAggregator : IProgress<FileCrypterProgress>
    {
        private readonly IReadOnlyList<string> sourcePaths;
        private readonly IProgress<BatchOperationProgress>? progress;
        private int completedFilesBeforeCurrent;
        private long lastInputBytes;

        public BatchProgressAggregator(
            IReadOnlyList<string> sourcePaths,
            IProgress<BatchOperationProgress>? progress)
        {
            this.sourcePaths = sourcePaths;
            this.progress = progress;
        }

        public void ReportStarted()
        {
            if (progress is null || sourcePaths.Count == 0)
            {
                return;
            }

            progress.Report(new BatchOperationProgress(
                0,
                sourcePaths.Count,
                CurrentInputPath: null,
                CurrentInputBytes: 0,
                CurrentTotalInputBytes: null,
                Percent: 0));
        }

        public void Report(FileCrypterProgress value)
        {
            if (progress is null || sourcePaths.Count == 0)
            {
                return;
            }

            if (value.InputBytes < lastInputBytes && completedFilesBeforeCurrent < sourcePaths.Count - 1)
            {
                completedFilesBeforeCurrent++;
            }

            lastInputBytes = value.InputBytes;

            double fileFraction = value.TotalInputBytes is > 0
                ? Math.Clamp(value.InputBytes / (double)value.TotalInputBytes.Value, 0, 1)
                : 0;
            double percent = Math.Clamp(
                ((completedFilesBeforeCurrent + fileFraction) / sourcePaths.Count) * 100d,
                0,
                100);
            string currentInputPath = sourcePaths[Math.Min(completedFilesBeforeCurrent, sourcePaths.Count - 1)];

            progress.Report(new BatchOperationProgress(
                completedFilesBeforeCurrent,
                sourcePaths.Count,
                currentInputPath,
                value.InputBytes,
                value.TotalInputBytes,
                percent));
        }

        public void ReportCompleted()
        {
            if (progress is null || sourcePaths.Count == 0)
            {
                return;
            }

            progress.Report(new BatchOperationProgress(
                sourcePaths.Count,
                sourcePaths.Count,
                CurrentInputPath: null,
                CurrentInputBytes: 0,
                CurrentTotalInputBytes: null,
                Percent: 100));
        }
    }
}

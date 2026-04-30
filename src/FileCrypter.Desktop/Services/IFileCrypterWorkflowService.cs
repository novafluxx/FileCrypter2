using FileCrypter.Core;

namespace FileCrypter.Desktop.Services;

public interface IFileCrypterWorkflowService
{
    Task<EncryptFileResult> EncryptFileAsync(
        EncryptFileRequest request,
        IProgress<FileCrypterProgress>? progress,
        CancellationToken cancellationToken);

    Task<DecryptFileResult> DecryptFileAsync(
        DecryptFileRequest request,
        IProgress<FileCrypterProgress>? progress,
        CancellationToken cancellationToken);

    Task<ArchiveEncryptResult> EncryptArchiveAsync(
        ArchiveEncryptRequest request,
        IProgress<FileCrypterProgress>? progress,
        CancellationToken cancellationToken);

    Task<ArchiveDecryptResult> DecryptArchiveAsync(
        ArchiveDecryptRequest request,
        IProgress<FileCrypterProgress>? progress,
        CancellationToken cancellationToken);

    Task<BatchTransformResult> EncryptFilesAsync(
        BatchTransformRequest request,
        IProgress<BatchOperationProgress>? progress,
        CancellationToken cancellationToken);

    Task<BatchTransformResult> DecryptFilesAsync(
        BatchTransformRequest request,
        IProgress<BatchOperationProgress>? progress,
        CancellationToken cancellationToken);
}

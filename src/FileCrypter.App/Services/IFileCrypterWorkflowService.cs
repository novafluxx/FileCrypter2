using FileCrypter.Core;

namespace FileCrypter.App.Services;

public interface IFileCrypterWorkflowService
{
    Task<EncryptFileResult> EncryptFileAsync(
        EncryptFileRequest request,
        IProgress<FileCrypterProgress>? progress,
        CancellationToken cancellationToken);
}

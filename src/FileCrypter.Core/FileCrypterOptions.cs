namespace FileCrypter.Core;

public sealed class FileCrypterOptions
{
    public int ChunkSize { get; init; } = (int)Format.FileCrypterFormatConstants.DefaultChunkSize;

    public int Argon2MemoryKiB { get; init; } = (int)Format.FileCrypterFormatConstants.DefaultArgon2MemoryKiB;

    public int Argon2Iterations { get; init; } = (int)Format.FileCrypterFormatConstants.DefaultArgon2Iterations;

    public int Argon2Parallelism { get; init; } = (int)Format.FileCrypterFormatConstants.DefaultArgon2Parallelism;

    public IProgress<FileCrypterProgress>? Progress { get; init; }

    internal IFileCrypterRandomSource? RandomSource { get; init; }
}

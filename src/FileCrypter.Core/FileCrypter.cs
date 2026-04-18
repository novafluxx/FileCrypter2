using System.Buffers.Binary;
using System.Formats.Tar;
using System.Security.Cryptography;
using FileCrypter.Core.Cryptography;
using FileCrypter.Core.Format;
using ZstdSharp;

namespace FileCrypter.Core;

public static class FileCrypter
{
    public const int DefaultGeneratedKeyFileSizeBytes = 32;
    public const int MaximumBatchFileCount = 1000;
    public const string DefaultArchiveEncryptedSuffix = ".tar.zst.encrypted";

    private const UnixFileMode PrivateFileMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
    private const int ZstdCompressionLevel = 3;
    private const string DefaultEncryptedSuffix = ".encrypted";

    public static byte[] GenerateKeyFileBytes(FileCrypterOptions? options = null)
    {
        return GenerateKeyFileBytes(DefaultGeneratedKeyFileSizeBytes, options);
    }

    public static byte[] GenerateKeyFileBytes(int byteCount, FileCrypterOptions? options = null)
    {
        ValidateGeneratedKeyFileByteCount(byteCount);

        byte[] keyFileBytes = new byte[byteCount];
        FillRandom(keyFileBytes, options ?? new FileCrypterOptions());
        return keyFileBytes;
    }

    public static Task<string> GenerateKeyFileAsync(
        string keyFilePath,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return GenerateKeyFileAsync(
            keyFilePath,
            DefaultGeneratedKeyFileSizeBytes,
            options,
            overwrite,
            cancellationToken);
    }

    public static async Task<string> GenerateKeyFileAsync(
        string keyFilePath,
        int byteCount,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrEmpty(keyFilePath);
        ValidateGeneratedKeyFileByteCount(byteCount);

        string fullKeyFilePath = Path.GetFullPath(keyFilePath);
        ValidateOutputPath(fullKeyFilePath, overwrite);
        if (!overwrite)
        {
            fullKeyFilePath = GetAvailableOutputPath(fullKeyFilePath);
        }

        string stagingPath = CreateStagingPath(fullKeyFilePath);
        byte[] keyFileBytes = GenerateKeyFileBytes(byteCount, options);
        bool completed = false;

        try
        {
            await using (FileStream output = new(
                stagingPath,
                CreateOutputFileStreamOptions()))
            {
                await output.WriteAsync(keyFileBytes, cancellationToken).ConfigureAwait(false);
            }

            File.Move(stagingPath, fullKeyFilePath, overwrite);
            completed = true;
            return fullKeyFilePath;
        }
        finally
        {
            CryptographicOperations.ZeroMemory(keyFileBytes);

            if (!completed)
            {
                TryDeleteFile(stagingPath);
            }
        }
    }

    public static Task<string> EncryptFileAsync(
        string plaintextPath,
        string encryptedPath,
        string password,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return TransformFileAsync(
            plaintextPath,
            encryptedPath,
            password,
            keyFilePath: null,
            options,
            overwrite,
            encrypt: true,
            cancellationToken);
    }

    public static Task<string> EncryptFileAsync(
        string plaintextPath,
        string encryptedPath,
        string password,
        string keyFilePath,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return TransformFileAsync(
            plaintextPath,
            encryptedPath,
            password,
            keyFilePath,
            options,
            overwrite,
            encrypt: true,
            cancellationToken);
    }

    public static Task<string> DecryptFileAsync(
        string encryptedPath,
        string plaintextPath,
        string password,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return TransformFileAsync(
            encryptedPath,
            plaintextPath,
            password,
            keyFilePath: null,
            options,
            overwrite,
            encrypt: false,
            cancellationToken);
    }

    public static Task<string> DecryptFileAsync(
        string encryptedPath,
        string plaintextPath,
        string password,
        string keyFilePath,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return TransformFileAsync(
            encryptedPath,
            plaintextPath,
            password,
            keyFilePath,
            options,
            overwrite,
            encrypt: false,
            cancellationToken);
    }

    public static Task<FileCrypterBatchResult> EncryptFilesAsync(
        IEnumerable<string> plaintextPaths,
        string outputDirectory,
        string password,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return TransformFilesAsync(
            plaintextPaths,
            outputDirectory,
            password,
            keyFilePath: null,
            options,
            overwrite,
            encrypt: true,
            cancellationToken);
    }

    public static Task<FileCrypterBatchResult> EncryptFilesAsync(
        IEnumerable<string> plaintextPaths,
        string outputDirectory,
        string password,
        string keyFilePath,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return TransformFilesAsync(
            plaintextPaths,
            outputDirectory,
            password,
            keyFilePath,
            options,
            overwrite,
            encrypt: true,
            cancellationToken);
    }

    public static Task<FileCrypterBatchResult> DecryptFilesAsync(
        IEnumerable<string> encryptedPaths,
        string outputDirectory,
        string password,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return TransformFilesAsync(
            encryptedPaths,
            outputDirectory,
            password,
            keyFilePath: null,
            options,
            overwrite,
            encrypt: false,
            cancellationToken);
    }

    public static Task<FileCrypterBatchResult> DecryptFilesAsync(
        IEnumerable<string> encryptedPaths,
        string outputDirectory,
        string password,
        string keyFilePath,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return TransformFilesAsync(
            encryptedPaths,
            outputDirectory,
            password,
            keyFilePath,
            options,
            overwrite,
            encrypt: false,
            cancellationToken);
    }

    public static Task<string> EncryptArchiveAsync(
        IEnumerable<string> plaintextPaths,
        string encryptedArchivePath,
        string password,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return EncryptArchiveCoreAsync(
            plaintextPaths,
            encryptedArchivePath,
            password,
            keyFilePath: null,
            options,
            overwrite,
            cancellationToken);
    }

    public static Task<string> EncryptArchiveAsync(
        IEnumerable<string> plaintextPaths,
        string encryptedArchivePath,
        string password,
        string keyFilePath,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return EncryptArchiveCoreAsync(
            plaintextPaths,
            encryptedArchivePath,
            password,
            keyFilePath,
            options,
            overwrite,
            cancellationToken);
    }

    public static Task<IReadOnlyList<string>> DecryptArchiveAsync(
        string encryptedArchivePath,
        string outputDirectory,
        string password,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return DecryptArchiveCoreAsync(
            encryptedArchivePath,
            outputDirectory,
            password,
            keyFilePath: null,
            options,
            overwrite,
            cancellationToken);
    }

    public static Task<IReadOnlyList<string>> DecryptArchiveAsync(
        string encryptedArchivePath,
        string outputDirectory,
        string password,
        string keyFilePath,
        FileCrypterOptions? options = null,
        bool overwrite = false,
        CancellationToken cancellationToken = default)
    {
        return DecryptArchiveCoreAsync(
            encryptedArchivePath,
            outputDirectory,
            password,
            keyFilePath,
            options,
            overwrite,
            cancellationToken);
    }

    public static async Task EncryptAsync(
        Stream plaintext,
        Stream encrypted,
        string password,
        FileCrypterOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EncryptAsyncCore(
            plaintext,
            encrypted,
            password,
            keyFileBytes: null,
            options,
            FileCrypterFormatConstants.PayloadKindSingleFile,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task EncryptAsync(
        Stream plaintext,
        Stream encrypted,
        string password,
        ReadOnlyMemory<byte> keyFileBytes,
        FileCrypterOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await EncryptAsyncCore(
            plaintext,
            encrypted,
            password,
            keyFileBytes,
            options,
            FileCrypterFormatConstants.PayloadKindSingleFile,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task EncryptAsyncCore(
        Stream plaintext,
        Stream encrypted,
        string password,
        ReadOnlyMemory<byte>? keyFileBytes,
        FileCrypterOptions? options,
        byte payloadKind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(encrypted);
        ArgumentNullException.ThrowIfNull(password);

        options ??= new FileCrypterOptions();
        ValidateEncryptionOptions(options);

        byte[] headerBytes = new byte[FileCrypterFormatConstants.HeaderLength];
        byte[] salt = new byte[FileCrypterFormatConstants.SaltLength];
        byte[] noncePrefix = new byte[FileCrypterFormatConstants.NoncePrefixLength];
        FillRandom(salt, options);
        FillRandom(noncePrefix, options);
        if (keyFileBytes.HasValue)
        {
            FileCrypterHeaderWriter.WriteKeyFileRequired(headerBytes, options, salt, noncePrefix, payloadKind);
        }
        else
        {
            FileCrypterHeaderWriter.WritePasswordOnly(headerBytes, options, salt, noncePrefix, payloadKind);
        }

        FileCrypterHeader header = FileCrypterHeaderParser.Parse(headerBytes);
        ReadOnlyMemory<byte> keyFileMaterial = keyFileBytes.GetValueOrDefault();
        byte[] key = FileCrypterKeyDeriver.DeriveKey(
            password,
            header,
            keyFileMaterial.Span,
            useKeyFile: keyFileBytes.HasValue);

        try
        {
            await encrypted.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

            using var aesGcm = new AesGcm(key, FileCrypterFormatConstants.AesGcmTagLength);
            using var chunkWriter = new ChunkEncryptingStream(encrypted, headerBytes, header, aesGcm, options);
            if (header.CompressionAlgorithmId == FileCrypterFormatConstants.CompressionZstd)
            {
                using (var compression = new CompressionStream(chunkWriter, ZstdCompressionLevel))
                {
                    await plaintext.CopyToAsync(compression, cancellationToken).ConfigureAwait(false);
                }

                await chunkWriter.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                await plaintext.CopyToAsync(chunkWriter, cancellationToken).ConfigureAwait(false);
                await chunkWriter.CompleteAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    public static async Task DecryptAsync(
        Stream encrypted,
        Stream plaintext,
        string password,
        FileCrypterOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await DecryptAsyncCore(
            encrypted,
            plaintext,
            password,
            keyFileBytes: null,
            options,
            FileCrypterFormatConstants.PayloadKindSingleFile,
            cancellationToken).ConfigureAwait(false);
    }

    public static async Task DecryptAsync(
        Stream encrypted,
        Stream plaintext,
        string password,
        ReadOnlyMemory<byte> keyFileBytes,
        FileCrypterOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        await DecryptAsyncCore(
            encrypted,
            plaintext,
            password,
            keyFileBytes,
            options,
            FileCrypterFormatConstants.PayloadKindSingleFile,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task DecryptAsyncCore(
        Stream encrypted,
        Stream plaintext,
        string password,
        ReadOnlyMemory<byte>? keyFileBytes,
        FileCrypterOptions? options,
        byte expectedPayloadKind,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(encrypted);
        ArgumentNullException.ThrowIfNull(plaintext);
        ArgumentNullException.ThrowIfNull(password);

        options ??= new FileCrypterOptions();

        byte[] headerBytes = new byte[FileCrypterFormatConstants.HeaderLength];
        int headerBytesRead = await ReadChunkAsync(encrypted, headerBytes, cancellationToken).ConfigureAwait(false);
        if (headerBytesRead < FileCrypterFormatConstants.HeaderLength)
        {
            FileCrypterHeaderParser.Parse(headerBytes.AsSpan(0, headerBytesRead));
        }

        FileCrypterHeader header = FileCrypterHeaderParser.Parse(headerBytes);
        ValidateSupportedPayload(header, keyFileBytes.HasValue, expectedPayloadKind);

        ReadOnlyMemory<byte> keyFileMaterial = keyFileBytes.GetValueOrDefault();
        byte[] key = FileCrypterKeyDeriver.DeriveKey(
            password,
            header,
            keyFileMaterial.Span,
            useKeyFile: header.IsKeyFileRequired);

        try
        {
            using var aesGcm = new AesGcm(key, FileCrypterFormatConstants.AesGcmTagLength);
            using var chunkReader = new ChunkDecryptingStream(encrypted, headerBytes, header, aesGcm, options);
            if (header.CompressionAlgorithmId == FileCrypterFormatConstants.CompressionZstd)
            {
                try
                {
                    using var decompression = new DecompressionStream(chunkReader);
                    await decompression.CopyToAsync(plaintext, cancellationToken).ConfigureAwait(false);
                }
                catch (ZstdException exception)
                {
                    throw new FileCrypterFormatException(
                        FileCrypterFormatErrorCode.InvalidCompressedPayload,
                        "The compressed FileCrypter payload is invalid.",
                        exception);
                }
            }
            else
            {
                await chunkReader.CopyToAsync(plaintext, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(key);
        }
    }

    private static async Task<string> TransformFileAsync(
        string inputPath,
        string outputPath,
        string password,
        string? keyFilePath,
        FileCrypterOptions? options,
        bool overwrite,
        bool encrypt,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(inputPath);
        ArgumentException.ThrowIfNullOrEmpty(outputPath);
        ArgumentNullException.ThrowIfNull(password);

        string fullInputPath = Path.GetFullPath(inputPath);
        string fullOutputPath = Path.GetFullPath(outputPath);
        string? fullKeyFilePath = keyFilePath is null ? null : Path.GetFullPath(keyFilePath);

        ValidateInputPath(fullInputPath);
        ValidateOutputPath(fullOutputPath, overwrite);
        if (fullKeyFilePath is not null)
        {
            ValidateKeyFilePath(fullKeyFilePath);
        }

        if (!overwrite)
        {
            fullOutputPath = GetAvailableOutputPath(fullOutputPath);
        }

        if (PathsEqual(ResolvePathForCollision(fullInputPath), ResolvePathForCollision(fullOutputPath)))
        {
            throw new ArgumentException("The input and output paths must be different.", nameof(outputPath));
        }

        if (fullKeyFilePath is not null &&
            PathsEqual(ResolvePathForCollision(fullKeyFilePath), ResolvePathForCollision(fullOutputPath)))
        {
            throw new ArgumentException("The key file and output paths must be different.", nameof(outputPath));
        }

        string stagingPath = CreateStagingPath(fullOutputPath);
        byte[]? keyFileBytes = null;
        bool completed = false;

        try
        {
            if (fullKeyFilePath is not null)
            {
                keyFileBytes = await ReadKeyFileAsync(fullKeyFilePath, cancellationToken).ConfigureAwait(false);
            }

            await using FileStream input = new(
                fullInputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);

            await using (FileStream output = new(
                stagingPath,
                CreateOutputFileStreamOptions()))
            {
                if (encrypt)
                {
                    if (keyFileBytes is null)
                    {
                        await EncryptAsync(input, output, password, options, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await EncryptAsync(input, output, password, keyFileBytes, options, cancellationToken).ConfigureAwait(false);
                    }
                }
                else
                {
                    if (keyFileBytes is null)
                    {
                        await DecryptAsync(input, output, password, options, cancellationToken).ConfigureAwait(false);
                    }
                    else
                    {
                        await DecryptAsync(input, output, password, keyFileBytes, options, cancellationToken).ConfigureAwait(false);
                    }
                }
            }

            File.Move(stagingPath, fullOutputPath, overwrite);
            completed = true;
            return fullOutputPath;
        }
        finally
        {
            if (keyFileBytes is not null)
            {
                CryptographicOperations.ZeroMemory(keyFileBytes);
            }

            if (!completed)
            {
                TryDeleteFile(stagingPath);
            }
        }
    }

    private static async Task<FileCrypterBatchResult> TransformFilesAsync(
        IEnumerable<string> inputPaths,
        string outputDirectory,
        string password,
        string? keyFilePath,
        FileCrypterOptions? options,
        bool overwrite,
        bool encrypt,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(inputPaths);
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
        ArgumentNullException.ThrowIfNull(password);

        string fullOutputDirectory = Path.GetFullPath(outputDirectory);
        ValidateBatchOutputDirectory(fullOutputDirectory);

        string[] inputPathArray = inputPaths as string[] ?? inputPaths.ToArray();
        ValidateBatchFileCount(inputPathArray.Length);

        FileCrypterOptions? transformOptions = encrypt
            ? CreateBatchEncryptionOptions(options)
            : options;
        var results = new List<FileCrypterBatchItemResult>(inputPathArray.Length);

        foreach (string inputPath in inputPathArray)
        {
            cancellationToken.ThrowIfCancellationRequested();

            string requestedOutputPath;
            try
            {
                ArgumentException.ThrowIfNullOrEmpty(inputPath);
                requestedOutputPath = CreateBatchOutputPath(inputPath, fullOutputDirectory, encrypt);
                string finalOutputPath;
                if (encrypt)
                {
                    finalOutputPath = keyFilePath is null
                        ? await EncryptFileAsync(
                            inputPath,
                            requestedOutputPath,
                            password,
                            transformOptions,
                            overwrite,
                            cancellationToken).ConfigureAwait(false)
                        : await EncryptFileAsync(
                            inputPath,
                            requestedOutputPath,
                            password,
                            keyFilePath,
                            transformOptions,
                            overwrite,
                            cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    finalOutputPath = keyFilePath is null
                        ? await DecryptFileAsync(
                            inputPath,
                            requestedOutputPath,
                            password,
                            transformOptions,
                            overwrite,
                            cancellationToken).ConfigureAwait(false)
                        : await DecryptFileAsync(
                            inputPath,
                            requestedOutputPath,
                            password,
                            keyFilePath,
                            transformOptions,
                            overwrite,
                            cancellationToken).ConfigureAwait(false);
                }

                results.Add(new FileCrypterBatchItemResult(
                    Path.GetFullPath(inputPath),
                    requestedOutputPath,
                    finalOutputPath,
                    Error: null));
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                requestedOutputPath = CreateFallbackBatchOutputPath(inputPath, fullOutputDirectory, encrypt);
                results.Add(new FileCrypterBatchItemResult(
                    inputPath,
                    requestedOutputPath,
                    OutputPath: null,
                    exception));
            }
        }

        return new FileCrypterBatchResult(results);
    }

    private static async Task<string> EncryptArchiveCoreAsync(
        IEnumerable<string> plaintextPaths,
        string encryptedArchivePath,
        string password,
        string? keyFilePath,
        FileCrypterOptions? options,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(plaintextPaths);
        ArgumentException.ThrowIfNullOrEmpty(encryptedArchivePath);
        ArgumentNullException.ThrowIfNull(password);

        string[] fullInputPaths = plaintextPaths
            .Select(path =>
            {
                ArgumentException.ThrowIfNullOrEmpty(path);
                return Path.GetFullPath(path);
            })
            .ToArray();
        ValidateArchiveInputFileCount(fullInputPaths.Length);
        foreach (string fullInputPath in fullInputPaths)
        {
            ValidateInputPath(fullInputPath);
            if (!File.Exists(fullInputPath))
            {
                throw new FileNotFoundException($"Input file does not exist: {fullInputPath}", fullInputPath);
            }
        }

        string fullOutputPath = Path.GetFullPath(encryptedArchivePath);
        string? fullKeyFilePath = keyFilePath is null ? null : Path.GetFullPath(keyFilePath);
        ValidateOutputPath(fullOutputPath, overwrite);
        if (fullKeyFilePath is not null)
        {
            ValidateKeyFilePath(fullKeyFilePath);
        }

        if (!overwrite)
        {
            fullOutputPath = GetAvailableOutputPath(fullOutputPath);
        }

        foreach (string fullInputPath in fullInputPaths)
        {
            if (PathsEqual(ResolvePathForCollision(fullInputPath), ResolvePathForCollision(fullOutputPath)))
            {
                throw new ArgumentException("The input and output paths must be different.", nameof(encryptedArchivePath));
            }
        }

        if (fullKeyFilePath is not null &&
            PathsEqual(ResolvePathForCollision(fullKeyFilePath), ResolvePathForCollision(fullOutputPath)))
        {
            throw new ArgumentException("The key file and output paths must be different.", nameof(encryptedArchivePath));
        }

        string tarStagingPath = CreateStagingPath(fullOutputPath + ".tar");
        string encryptedStagingPath = CreateStagingPath(fullOutputPath);
        byte[]? keyFileBytes = null;
        bool completed = false;

        try
        {
            if (fullKeyFilePath is not null)
            {
                keyFileBytes = await ReadKeyFileAsync(fullKeyFilePath, cancellationToken).ConfigureAwait(false);
            }

            await using (FileStream tarOutput = new(tarStagingPath, CreateOutputFileStreamOptions()))
            {
                await WriteTarArchiveAsync(fullInputPaths, tarOutput, cancellationToken).ConfigureAwait(false);
            }

            await using FileStream tarInput = new(
                tarStagingPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);
            await using (FileStream encryptedOutput = new(
                encryptedStagingPath,
                CreateOutputFileStreamOptions()))
            {
                FileCrypterOptions archiveOptions = CreateArchiveEncryptionOptions(options);
                if (keyFileBytes is null)
                {
                    await EncryptAsyncCore(
                        tarInput,
                        encryptedOutput,
                        password,
                        keyFileBytes: null,
                        archiveOptions,
                        FileCrypterFormatConstants.PayloadKindTarArchive,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await EncryptAsyncCore(
                        tarInput,
                        encryptedOutput,
                        password,
                        keyFileBytes,
                        archiveOptions,
                        FileCrypterFormatConstants.PayloadKindTarArchive,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            File.Move(encryptedStagingPath, fullOutputPath, overwrite);
            completed = true;
            return fullOutputPath;
        }
        finally
        {
            if (keyFileBytes is not null)
            {
                CryptographicOperations.ZeroMemory(keyFileBytes);
            }

            TryDeleteFile(tarStagingPath);
            if (!completed)
            {
                TryDeleteFile(encryptedStagingPath);
            }
        }
    }

    private static async Task<IReadOnlyList<string>> DecryptArchiveCoreAsync(
        string encryptedArchivePath,
        string outputDirectory,
        string password,
        string? keyFilePath,
        FileCrypterOptions? options,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(encryptedArchivePath);
        ArgumentException.ThrowIfNullOrEmpty(outputDirectory);
        ArgumentNullException.ThrowIfNull(password);

        string fullEncryptedArchivePath = Path.GetFullPath(encryptedArchivePath);
        string fullOutputDirectory = Path.GetFullPath(outputDirectory);
        string? fullKeyFilePath = keyFilePath is null ? null : Path.GetFullPath(keyFilePath);
        ValidateInputPath(fullEncryptedArchivePath);
        ValidateBatchOutputDirectory(fullOutputDirectory);
        if (fullKeyFilePath is not null)
        {
            ValidateKeyFilePath(fullKeyFilePath);
        }

        string tarStagingPath = CreateStagingPath(Path.Combine(fullOutputDirectory, "archive.tar"));
        byte[]? keyFileBytes = null;

        try
        {
            if (fullKeyFilePath is not null)
            {
                keyFileBytes = await ReadKeyFileAsync(fullKeyFilePath, cancellationToken).ConfigureAwait(false);
            }

            await using FileStream encryptedInput = new(
                fullEncryptedArchivePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);
            await using (FileStream tarOutput = new(tarStagingPath, CreateOutputFileStreamOptions()))
            {
                if (keyFileBytes is null)
                {
                    await DecryptAsyncCore(
                        encryptedInput,
                        tarOutput,
                        password,
                        keyFileBytes: null,
                        options,
                        FileCrypterFormatConstants.PayloadKindTarArchive,
                        cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await DecryptAsyncCore(
                        encryptedInput,
                        tarOutput,
                        password,
                        keyFileBytes,
                        options,
                        FileCrypterFormatConstants.PayloadKindTarArchive,
                        cancellationToken).ConfigureAwait(false);
                }
            }

            return await ExtractTarArchiveAsync(
                tarStagingPath,
                fullOutputDirectory,
                overwrite,
                cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            if (keyFileBytes is not null)
            {
                CryptographicOperations.ZeroMemory(keyFileBytes);
            }

            TryDeleteFile(tarStagingPath);
        }
    }

    private static void ValidateInputPath(string fullInputPath)
    {
        if (Directory.Exists(fullInputPath))
        {
            throw new IOException("The input path points to a directory.");
        }

        if (IsSymbolicLink(fullInputPath))
        {
            throw new IOException("The input path must not be a symbolic link.");
        }
    }

    private static void ValidateBatchOutputDirectory(string fullOutputDirectory)
    {
        if (File.Exists(fullOutputDirectory))
        {
            throw new IOException("The batch output directory path points to a file.");
        }

        if (!Directory.Exists(fullOutputDirectory))
        {
            throw new DirectoryNotFoundException($"The batch output directory does not exist: {fullOutputDirectory}");
        }
    }

    private static void ValidateBatchFileCount(int fileCount)
    {
        if (fileCount > MaximumBatchFileCount)
        {
            throw new ArgumentException(
                $"A single batch run supports up to {MaximumBatchFileCount} files.",
                nameof(fileCount));
        }
    }

    private static void ValidateArchiveInputFileCount(int fileCount)
    {
        if (fileCount == 0)
        {
            throw new ArgumentException("At least one input file is required.", nameof(fileCount));
        }

        ValidateBatchFileCount(fileCount);
    }

    private static FileCrypterOptions CreateBatchEncryptionOptions(FileCrypterOptions? options)
    {
        return CreateCompressionEnabledOptions(options);
    }

    private static FileCrypterOptions CreateArchiveEncryptionOptions(FileCrypterOptions? options)
    {
        return CreateCompressionEnabledOptions(options);
    }

    private static FileCrypterOptions CreateCompressionEnabledOptions(FileCrypterOptions? options)
    {
        FileCrypterOptions sourceOptions = options ?? new FileCrypterOptions();
        return new FileCrypterOptions
        {
            ChunkSize = sourceOptions.ChunkSize,
            Argon2MemoryKiB = sourceOptions.Argon2MemoryKiB,
            Argon2Iterations = sourceOptions.Argon2Iterations,
            Argon2Parallelism = sourceOptions.Argon2Parallelism,
            EnableCompression = true,
            Progress = sourceOptions.Progress,
            RandomSource = sourceOptions.RandomSource,
        };
    }

    private static async Task WriteTarArchiveAsync(
        IEnumerable<string> fullInputPaths,
        Stream tarOutput,
        CancellationToken cancellationToken)
    {
        using var writer = new TarWriter(tarOutput, TarEntryFormat.Pax, leaveOpen: true);
        var entryNames = new HashSet<string>(StringComparer.Ordinal);

        foreach (string fullInputPath in fullInputPaths)
        {
            string entryName = GetAvailableArchiveEntryName(Path.GetFileName(fullInputPath), entryNames);
            await writer.WriteEntryAsync(fullInputPath, entryName, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task<IReadOnlyList<string>> ExtractTarArchiveAsync(
        string tarPath,
        string fullOutputDirectory,
        bool overwrite,
        CancellationToken cancellationToken)
    {
        var stagedExtractions = new List<StagedArchiveExtraction>();
        var reservedFinalPaths = new HashSet<string>(PathComparer);
        bool completed = false;

        try
        {
            await using FileStream tarInput = new(
                tarPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);
            using var reader = new TarReader(tarInput, leaveOpen: false);

            while (await reader.GetNextEntryAsync(copyData: false, cancellationToken).ConfigureAwait(false) is { } entry)
            {
                if (!IsRegularFileEntry(entry))
                {
                    throw new FileCrypterFormatException(
                        FileCrypterFormatErrorCode.InvalidArchivePayload,
                        "The FileCrypter archive contains an unsupported tar entry.");
                }

                if (entry.DataStream is null)
                {
                    throw new FileCrypterFormatException(
                        FileCrypterFormatErrorCode.InvalidArchivePayload,
                        "The FileCrypter archive contains a file entry without data.");
                }

                string requestedOutputPath = CreateArchiveExtractionOutputPath(entry.Name, fullOutputDirectory);
                string finalOutputPath = overwrite
                    ? requestedOutputPath
                    : GetAvailableOutputPath(requestedOutputPath, reservedFinalPaths);
                ValidateOutputPath(finalOutputPath, overwrite);
                if (!reservedFinalPaths.Add(NormalizePathForComparison(finalOutputPath)))
                {
                    throw new FileCrypterFormatException(
                        FileCrypterFormatErrorCode.InvalidArchivePayload,
                        "The FileCrypter archive contains duplicate extraction paths.");
                }

                string stagingPath = CreateStagingPath(finalOutputPath);
                await using (FileStream output = new(stagingPath, CreateOutputFileStreamOptions()))
                {
                    await entry.DataStream.CopyToAsync(output, cancellationToken).ConfigureAwait(false);
                }

                stagedExtractions.Add(new StagedArchiveExtraction(stagingPath, finalOutputPath));
            }

            foreach (StagedArchiveExtraction extraction in stagedExtractions)
            {
                File.Move(extraction.StagingPath, extraction.FinalPath, overwrite);
            }

            completed = true;
            return stagedExtractions.Select(extraction => extraction.FinalPath).ToArray();
        }
        catch (Exception exception) when (exception is InvalidDataException or EndOfStreamException)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidArchivePayload,
                "The FileCrypter archive payload is invalid.",
                exception);
        }
        finally
        {
            if (!completed)
            {
                foreach (StagedArchiveExtraction extraction in stagedExtractions)
                {
                    TryDeleteFile(extraction.StagingPath);
                }
            }
        }
    }

    private static bool IsRegularFileEntry(TarEntry entry)
    {
        return entry.EntryType is TarEntryType.RegularFile
            or TarEntryType.V7RegularFile
            or TarEntryType.ContiguousFile;
    }

    private static string CreateArchiveExtractionOutputPath(string entryName, string fullOutputDirectory)
    {
        string fileName = Path.GetFileName(entryName);
        if (string.IsNullOrEmpty(fileName) ||
            !string.Equals(entryName, fileName, StringComparison.Ordinal) ||
            Path.IsPathRooted(entryName) ||
            entryName.Contains('\\', StringComparison.Ordinal) ||
            entryName.Contains('/', StringComparison.Ordinal) ||
            entryName == "." ||
            entryName == "..")
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidArchivePayload,
                "The FileCrypter archive contains an unsafe entry path.");
        }

        string outputPath = Path.GetFullPath(Path.Combine(fullOutputDirectory, fileName));
        string fullOutputDirectoryWithSeparator = Path.EndsInDirectorySeparator(fullOutputDirectory)
            ? fullOutputDirectory
            : fullOutputDirectory + Path.DirectorySeparatorChar;

        if (!outputPath.StartsWith(fullOutputDirectoryWithSeparator, PathComparison))
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidArchivePayload,
                "The FileCrypter archive contains an unsafe entry path.");
        }

        return outputPath;
    }

    private static string GetAvailableArchiveEntryName(string entryName, HashSet<string> usedEntryNames)
    {
        if (string.IsNullOrEmpty(entryName))
        {
            throw new ArgumentException("The input path must include a file name.", nameof(entryName));
        }

        if (usedEntryNames.Add(entryName))
        {
            return entryName;
        }

        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(entryName);
        string extension = Path.GetExtension(entryName);
        for (int suffix = 1; suffix < int.MaxValue; suffix++)
        {
            string candidateName = $"{fileNameWithoutExtension} ({suffix}){extension}";
            if (usedEntryNames.Add(candidateName))
            {
                return candidateName;
            }
        }

        throw new IOException("No available archive entry name could be found.");
    }

    private static string CreateBatchOutputPath(string inputPath, string fullOutputDirectory, bool encrypt)
    {
        string inputFileName = Path.GetFileName(inputPath);
        if (string.IsNullOrEmpty(inputFileName))
        {
            throw new ArgumentException("The input path must include a file name.", nameof(inputPath));
        }

        string outputFileName = encrypt
            ? inputFileName + DefaultEncryptedSuffix
            : GetDefaultBatchDecryptedFileName(inputFileName);
        return Path.Combine(fullOutputDirectory, outputFileName);
    }

    private static string CreateFallbackBatchOutputPath(string inputPath, string fullOutputDirectory, bool encrypt)
    {
        if (string.IsNullOrEmpty(inputPath))
        {
            return Path.Combine(
                fullOutputDirectory,
                encrypt ? "invalid-input.encrypted" : "invalid-input.decrypted");
        }

        try
        {
            return CreateBatchOutputPath(inputPath, fullOutputDirectory, encrypt);
        }
        catch (ArgumentException)
        {
            return Path.Combine(
                fullOutputDirectory,
                encrypt ? "invalid-input.encrypted" : "invalid-input.decrypted");
        }
    }

    private static string GetDefaultBatchDecryptedFileName(string inputFileName)
    {
        return inputFileName.EndsWith(DefaultEncryptedSuffix, StringComparison.OrdinalIgnoreCase)
            ? inputFileName[..^DefaultEncryptedSuffix.Length]
            : inputFileName + ".decrypted";
    }

    private static void ValidateKeyFilePath(string fullKeyFilePath)
    {
        if (Directory.Exists(fullKeyFilePath))
        {
            throw new IOException("The key file path points to a directory.");
        }

        if (IsSymbolicLink(fullKeyFilePath))
        {
            throw new IOException("The key file path must not be a symbolic link.");
        }
    }

    private static void ValidateOutputPath(string fullOutputPath, bool overwrite)
    {
        string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
        string outputFileName = Path.GetFileName(fullOutputPath);

        if (string.IsNullOrEmpty(outputDirectory) || string.IsNullOrEmpty(outputFileName))
        {
            throw new ArgumentException("The output path must include a file name.", nameof(fullOutputPath));
        }

        if (!Directory.Exists(outputDirectory))
        {
            throw new DirectoryNotFoundException($"The output directory does not exist: {outputDirectory}");
        }

        if (Directory.Exists(fullOutputPath))
        {
            throw new IOException("The output path points to a directory.");
        }

        if (overwrite && IsSymbolicLink(fullOutputPath))
        {
            throw new IOException("The output path must not be a symbolic link.");
        }
    }

    private static FileStreamOptions CreateOutputFileStreamOptions()
    {
        var options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = 81920,
            Options = FileOptions.SequentialScan,
        };

        if (!OperatingSystem.IsWindows())
        {
            options.UnixCreateMode = PrivateFileMode;
        }

        return options;
    }

    private static async Task<byte[]> ReadKeyFileAsync(string fullKeyFilePath, CancellationToken cancellationToken)
    {
        await using FileStream keyFile = new(
            fullKeyFilePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: 81920,
            FileOptions.SequentialScan);

        if (keyFile.Length > FileCrypterFormatConstants.MaximumKeyFileSizeBytes)
        {
            throw new IOException(
                $"The key file is too large. The maximum supported key file size is {FileCrypterFormatConstants.MaximumKeyFileSizeBytes} bytes.");
        }

        byte[] keyFileBytes = new byte[checked((int)keyFile.Length)];
        int bytesRead = await ReadChunkAsync(keyFile, keyFileBytes, cancellationToken).ConfigureAwait(false);
        if (bytesRead != keyFileBytes.Length)
        {
            throw new IOException("The key file could not be read completely.");
        }

        byte[] trailingByte = new byte[1];
        int trailingBytesRead = await keyFile.ReadAsync(trailingByte, cancellationToken).ConfigureAwait(false);
        if (trailingBytesRead != 0)
        {
            CryptographicOperations.ZeroMemory(keyFileBytes);
            throw new IOException(
                $"The key file is too large. The maximum supported key file size is {FileCrypterFormatConstants.MaximumKeyFileSizeBytes} bytes.");
        }

        return keyFileBytes;
    }

    private static bool IsSymbolicLink(string path)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.LinkTarget is not null)
        {
            return true;
        }

        var directoryInfo = new DirectoryInfo(path);
        return directoryInfo.LinkTarget is not null;
    }

    private static void ValidateEncryptionOptions(FileCrypterOptions options)
    {
        if (options.ChunkSize < FileCrypterFormatConstants.MinimumChunkSize ||
            options.ChunkSize > FileCrypterFormatConstants.MaximumChunkSize ||
            options.ChunkSize % FileCrypterFormatConstants.ChunkSizeMultiple != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The chunk size is outside the FileCrypter v1 range.");
        }

        if (options.Argon2MemoryKiB <= 0 || options.Argon2Iterations <= 0 || options.Argon2Parallelism <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(options), "The Argon2 parameters must be positive.");
        }
    }

    private static void ValidateGeneratedKeyFileByteCount(int byteCount)
    {
        if (byteCount < DefaultGeneratedKeyFileSizeBytes ||
            byteCount > FileCrypterFormatConstants.MaximumKeyFileSizeBytes)
        {
            throw new ArgumentOutOfRangeException(
                nameof(byteCount),
                $"Generated key files must be between {DefaultGeneratedKeyFileSizeBytes} and {FileCrypterFormatConstants.MaximumKeyFileSizeBytes} bytes.");
        }
    }

    private static void FillRandom(Span<byte> destination, FileCrypterOptions options)
    {
        if (options.RandomSource is not null)
        {
            options.RandomSource.Fill(destination);
            return;
        }

        RandomNumberGenerator.Fill(destination);
    }

    private static string CreateStagingPath(string fullOutputPath)
    {
        string? outputDirectory = Path.GetDirectoryName(fullOutputPath);
        string outputFileName = Path.GetFileName(fullOutputPath);

        if (string.IsNullOrEmpty(outputDirectory) || string.IsNullOrEmpty(outputFileName))
        {
            throw new ArgumentException("The output path must include a file name.", nameof(fullOutputPath));
        }

        return Path.Combine(outputDirectory, $".{outputFileName}.{Guid.NewGuid():N}.tmp");
    }

    private static string GetAvailableOutputPath(string fullOutputPath)
    {
        return GetAvailableOutputPath(fullOutputPath, reservedPaths: null);
    }

    private static string GetAvailableOutputPath(string fullOutputPath, HashSet<string>? reservedPaths)
    {
        if (!PathExistsOrSymbolicLink(fullOutputPath))
        {
            string normalizedPath = NormalizePathForComparison(fullOutputPath);
            if (reservedPaths is null || !reservedPaths.Contains(normalizedPath))
            {
                return fullOutputPath;
            }
        }

        string? directory = Path.GetDirectoryName(fullOutputPath);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(fullOutputPath);
        string extension = Path.GetExtension(fullOutputPath);

        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileNameWithoutExtension))
        {
            throw new ArgumentException("The output path must include a file name.", nameof(fullOutputPath));
        }

        for (int suffix = 1; suffix < int.MaxValue; suffix++)
        {
            string candidatePath = Path.Combine(directory, $"{fileNameWithoutExtension} ({suffix}){extension}");
            string normalizedPath = NormalizePathForComparison(candidatePath);
            if (!PathExistsOrSymbolicLink(candidatePath) &&
                (reservedPaths is null || !reservedPaths.Contains(normalizedPath)))
            {
                return candidatePath;
            }
        }

        throw new IOException("No available auto-renamed output path could be found.");
    }

    private static bool PathExistsOrSymbolicLink(string path)
    {
        return File.Exists(path) || Directory.Exists(path) || IsSymbolicLink(path);
    }

    private static string ResolvePathForCollision(string fullPath)
    {
        string? directory = Path.GetDirectoryName(fullPath);
        string fileName = Path.GetFileName(fullPath);
        if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(fileName))
        {
            return fullPath;
        }

        string resolvedDirectory = ResolveExistingPath(directory);
        string pathInResolvedDirectory = Path.Combine(resolvedDirectory, fileName);
        return PathExistsOrSymbolicLink(pathInResolvedDirectory)
            ? ResolveExistingPath(pathInResolvedDirectory)
            : Path.GetFullPath(pathInResolvedDirectory);
    }

    private static string ResolveExistingPath(string fullPath)
    {
        string? root = Path.GetPathRoot(fullPath);
        if (string.IsNullOrEmpty(root))
        {
            return Path.GetFullPath(fullPath);
        }

        string relativePath = Path.GetRelativePath(root, fullPath);
        if (relativePath == ".")
        {
            return root;
        }

        string currentPath = root;
        string[] pathParts = relativePath.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);

        foreach (string pathPart in pathParts)
        {
            string candidatePath = Path.Combine(currentPath, pathPart);
            string? targetPath = GetResolvedSymbolicLinkTarget(candidatePath);
            currentPath = targetPath ?? candidatePath;
        }

        return Path.GetFullPath(currentPath);
    }

    private static string? GetResolvedSymbolicLinkTarget(string path)
    {
        var fileInfo = new FileInfo(path);
        if (fileInfo.LinkTarget is not null)
        {
            return ResolveSymbolicLinkTarget(fileInfo, path);
        }

        var directoryInfo = new DirectoryInfo(path);
        return directoryInfo.LinkTarget is not null
            ? ResolveSymbolicLinkTarget(directoryInfo, path)
            : null;
    }

    private static string ResolveSymbolicLinkTarget(FileSystemInfo linkInfo, string linkPath)
    {
        FileSystemInfo? resolvedTarget = linkInfo.ResolveLinkTarget(returnFinalTarget: true);
        if (resolvedTarget is not null)
        {
            return resolvedTarget.FullName;
        }

        string linkTarget = linkInfo.LinkTarget ?? throw new IOException($"The symbolic link target could not be read: {linkPath}");
        return Path.GetFullPath(
            Path.IsPathRooted(linkTarget)
                ? linkTarget
                : Path.Combine(Path.GetDirectoryName(linkPath) ?? string.Empty, linkTarget));
    }

    private static StringComparer PathComparer => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    private static StringComparison PathComparison => OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparison.OrdinalIgnoreCase
        : StringComparison.Ordinal;

    private static string NormalizePathForComparison(string path)
    {
        return Path.GetFullPath(path);
    }

    private static bool PathsEqual(string left, string right)
    {
        return string.Equals(left, right, PathComparison);
    }

    private static void TryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
        }
    }

    private static void ValidateSupportedPayload(FileCrypterHeader header, bool keyFileSupplied, byte expectedPayloadKind)
    {
        if (header.IsKeyFileRequired && !keyFileSupplied)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.KeyFileRequired,
                "This FileCrypter payload requires the matching key file.");
        }

        if (header.CompressionAlgorithmId is not FileCrypterFormatConstants.CompressionNone
            and not FileCrypterFormatConstants.CompressionZstd)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedCompressionAlgorithm,
                "This FileCrypter operation does not support the payload compression algorithm.");
        }

        if (header.PayloadKind != expectedPayloadKind)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedPayloadKind,
                expectedPayloadKind == FileCrypterFormatConstants.PayloadKindTarArchive
                    ? "This FileCrypter operation only supports archive payloads."
                    : "This FileCrypter operation only supports single-file payloads.");
        }

        if (header.Argon2MemoryKiB > int.MaxValue ||
            header.Argon2Iterations > int.MaxValue ||
            header.Argon2Parallelism > int.MaxValue)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.InvalidArgon2Parameters,
                "The FileCrypter Argon2 parameters are invalid.");
        }
    }

    private sealed record StagedArchiveExtraction(string StagingPath, string FinalPath);

    private static async ValueTask<int> ReadChunkAsync(
        Stream source,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < buffer.Length)
        {
            int bytesRead = await source.ReadAsync(buffer.Slice(totalBytesRead), cancellationToken).ConfigureAwait(false);
            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead += bytesRead;
        }

        return totalBytesRead;
    }

    private static async ValueTask ReadExactChunkBytesAsync(
        Stream source,
        Memory<byte> buffer,
        CancellationToken cancellationToken)
    {
        int bytesRead = await ReadChunkAsync(source, buffer, cancellationToken).ConfigureAwait(false);
        if (bytesRead != buffer.Length)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.TruncatedChunk,
                "The FileCrypter chunk is truncated.");
        }
    }

    private static void WriteChunkPrefix(Span<byte> destination, uint plaintextLength, bool isFinal)
    {
        BinaryPrimitives.WriteUInt32LittleEndian(destination.Slice(0, sizeof(uint)), plaintextLength);
        BinaryPrimitives.WriteUInt16LittleEndian(
            destination.Slice(4, sizeof(ushort)),
            isFinal ? FileCrypterFormatConstants.ChunkFlagFinal : (ushort)0);
        BinaryPrimitives.WriteUInt16LittleEndian(destination.Slice(6, sizeof(ushort)), 0);
    }

    private sealed class ChunkEncryptingStream : Stream
    {
        private readonly Stream output;
        private readonly FileCrypterHeader header;
        private readonly AesGcm aesGcm;
        private readonly FileCrypterOptions options;
        private readonly byte[] plaintextBuffer;
        private readonly byte[] ciphertextBuffer;
        private readonly byte[] tag = new byte[FileCrypterFormatConstants.AesGcmTagLength];
        private readonly byte[] prefix = new byte[FileCrypterFormatConstants.ChunkFramePrefixLength];
        private readonly byte[] nonce = new byte[FileCrypterFormatConstants.AesGcmNonceLength];
        private readonly byte[] associatedData = new byte[
            FileCrypterFormatConstants.HeaderLength + FileCrypterFormatConstants.ChunkFramePrefixLength];
        private int bufferedBytes;
        private uint chunkIndex;
        private long inputBytes;
        private long outputBytes;
        private bool completed;

        public ChunkEncryptingStream(
            Stream output,
            ReadOnlySpan<byte> headerBytes,
            FileCrypterHeader header,
            AesGcm aesGcm,
            FileCrypterOptions options)
        {
            this.output = output;
            this.header = header;
            this.aesGcm = aesGcm;
            this.options = options;
            plaintextBuffer = new byte[checked((int)header.ChunkSize)];
            ciphertextBuffer = new byte[checked((int)header.ChunkSize)];
            outputBytes = headerBytes.Length;
            headerBytes.CopyTo(associatedData);
            header.NoncePrefix.CopyTo(nonce);
        }

        public override bool CanRead => false;

        public override bool CanSeek => false;

        public override bool CanWrite => !completed;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public async ValueTask CompleteAsync(CancellationToken cancellationToken)
        {
            if (completed)
            {
                return;
            }

            await WriteEncryptedChunkAsync(bufferedBytes, isFinal: true, cancellationToken).ConfigureAwait(false);
            bufferedBytes = 0;
            completed = true;
        }

        public override void Flush()
        {
            output.Flush();
        }

        public override Task FlushAsync(CancellationToken cancellationToken)
        {
            return output.FlushAsync(cancellationToken);
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            Write(buffer.AsSpan(offset, count));
        }

        public override void Write(ReadOnlySpan<byte> buffer)
        {
            ThrowIfCompleted();

            while (!buffer.IsEmpty)
            {
                int bytesToCopy = Math.Min(plaintextBuffer.Length - bufferedBytes, buffer.Length);
                buffer.Slice(0, bytesToCopy).CopyTo(plaintextBuffer.AsSpan(bufferedBytes));
                bufferedBytes += bytesToCopy;
                buffer = buffer.Slice(bytesToCopy);

                if (bufferedBytes == plaintextBuffer.Length)
                {
                    WriteEncryptedChunk(bufferedBytes, isFinal: false);
                    bufferedBytes = 0;
                    AdvanceChunkIndex();
                }
            }
        }

        public override async ValueTask WriteAsync(
            ReadOnlyMemory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            ThrowIfCompleted();

            while (!buffer.IsEmpty)
            {
                int bytesToCopy = Math.Min(plaintextBuffer.Length - bufferedBytes, buffer.Length);
                buffer.Slice(0, bytesToCopy).CopyTo(plaintextBuffer.AsMemory(bufferedBytes));
                bufferedBytes += bytesToCopy;
                buffer = buffer.Slice(bytesToCopy);

                if (bufferedBytes == plaintextBuffer.Length)
                {
                    await WriteEncryptedChunkAsync(bufferedBytes, isFinal: false, cancellationToken).ConfigureAwait(false);
                    bufferedBytes = 0;
                    AdvanceChunkIndex();
                }
            }
        }

        private void WriteEncryptedChunk(int plaintextLength, bool isFinal)
        {
            EncryptChunk(plaintextLength, isFinal);
            output.Write(prefix);
            output.Write(ciphertextBuffer.AsSpan(0, plaintextLength));
            output.Write(tag);
            ReportProgress(plaintextLength);
        }

        private async ValueTask WriteEncryptedChunkAsync(
            int plaintextLength,
            bool isFinal,
            CancellationToken cancellationToken)
        {
            EncryptChunk(plaintextLength, isFinal);
            await output.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(ciphertextBuffer.AsMemory(0, plaintextLength), cancellationToken).ConfigureAwait(false);
            await output.WriteAsync(tag, cancellationToken).ConfigureAwait(false);
            ReportProgress(plaintextLength);
        }

        private void EncryptChunk(int plaintextLength, bool isFinal)
        {
            WriteChunkPrefix(prefix, (uint)plaintextLength, isFinal);
            prefix.CopyTo(associatedData, FileCrypterFormatConstants.HeaderLength);
            BinaryPrimitives.WriteUInt32LittleEndian(
                nonce.AsSpan(FileCrypterFormatConstants.NoncePrefixLength, sizeof(uint)),
                chunkIndex);

            aesGcm.Encrypt(
                nonce,
                plaintextBuffer.AsSpan(0, plaintextLength),
                ciphertextBuffer.AsSpan(0, plaintextLength),
                tag,
                associatedData);
        }

        private void AdvanceChunkIndex()
        {
            if (chunkIndex == uint.MaxValue)
            {
                throw new FileCrypterFormatException(
                    FileCrypterFormatErrorCode.TooManyChunks,
                    "The input is too large for the FileCrypter v1 chunk counter.");
            }

            chunkIndex++;
        }

        private void ReportProgress(int plaintextLength)
        {
            inputBytes += plaintextLength;
            outputBytes += prefix.Length + plaintextLength + tag.Length;
            options.Progress?.Report(new FileCrypterProgress(inputBytes, outputBytes));
        }

        private void ThrowIfCompleted()
        {
            if (completed)
            {
                throw new ObjectDisposedException(nameof(ChunkEncryptingStream));
            }
        }
    }

    private sealed class ChunkDecryptingStream : Stream
    {
        private readonly Stream input;
        private readonly FileCrypterHeader header;
        private readonly AesGcm aesGcm;
        private readonly FileCrypterOptions options;
        private readonly byte[] prefix = new byte[FileCrypterFormatConstants.ChunkFramePrefixLength];
        private readonly byte[] nonce = new byte[FileCrypterFormatConstants.AesGcmNonceLength];
        private readonly byte[] associatedData = new byte[
            FileCrypterFormatConstants.HeaderLength + FileCrypterFormatConstants.ChunkFramePrefixLength];
        private readonly byte[] trailingByte = new byte[1];
        private readonly byte[] ciphertextBuffer;
        private readonly byte[] plaintextBuffer;
        private readonly byte[] tag = new byte[FileCrypterFormatConstants.AesGcmTagLength];
        private int plaintextOffset;
        private int plaintextLength;
        private uint chunkIndex;
        private long inputBytes;
        private long outputBytes;
        private bool completed;

        public ChunkDecryptingStream(
            Stream input,
            ReadOnlySpan<byte> headerBytes,
            FileCrypterHeader header,
            AesGcm aesGcm,
            FileCrypterOptions options)
        {
            this.input = input;
            this.header = header;
            this.aesGcm = aesGcm;
            this.options = options;
            int chunkSize = checked((int)header.ChunkSize);
            ciphertextBuffer = new byte[chunkSize];
            plaintextBuffer = new byte[chunkSize];
            inputBytes = headerBytes.Length;
            headerBytes.CopyTo(associatedData);
            header.NoncePrefix.CopyTo(nonce);
        }

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            return Read(buffer.AsSpan(offset, count));
        }

        public override int Read(Span<byte> buffer)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }

            if (!EnsurePlaintextAvailable())
            {
                return 0;
            }

            int bytesToCopy = Math.Min(buffer.Length, plaintextLength - plaintextOffset);
            plaintextBuffer.AsSpan(plaintextOffset, bytesToCopy).CopyTo(buffer);
            plaintextOffset += bytesToCopy;
            return bytesToCopy;
        }

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (buffer.IsEmpty)
            {
                return 0;
            }

            if (!await EnsurePlaintextAvailableAsync(cancellationToken).ConfigureAwait(false))
            {
                return 0;
            }

            int bytesToCopy = Math.Min(buffer.Length, plaintextLength - plaintextOffset);
            plaintextBuffer.AsMemory(plaintextOffset, bytesToCopy).CopyTo(buffer);
            plaintextOffset += bytesToCopy;
            return bytesToCopy;
        }

        public override long Seek(long offset, SeekOrigin origin)
        {
            throw new NotSupportedException();
        }

        public override void SetLength(long value)
        {
            throw new NotSupportedException();
        }

        public override void Write(byte[] buffer, int offset, int count)
        {
            throw new NotSupportedException();
        }

        private bool EnsurePlaintextAvailable()
        {
            while (plaintextOffset >= plaintextLength)
            {
                if (completed)
                {
                    return false;
                }

                ReadNextChunk();
            }

            return true;
        }

        private async ValueTask<bool> EnsurePlaintextAvailableAsync(CancellationToken cancellationToken)
        {
            while (plaintextOffset >= plaintextLength)
            {
                if (completed)
                {
                    return false;
                }

                await ReadNextChunkAsync(cancellationToken).ConfigureAwait(false);
            }

            return true;
        }

        private void ReadNextChunk()
        {
            ReadExactChunkBytes(input, prefix);
            int chunkPlaintextLength = ValidateChunkPrefix();
            ReadExactChunkBytes(input, ciphertextBuffer.AsSpan(0, chunkPlaintextLength));
            ReadExactChunkBytes(input, tag);
            DecryptChunk(chunkPlaintextLength);
            CompleteOrAdvanceChunk();
        }

        private async ValueTask ReadNextChunkAsync(CancellationToken cancellationToken)
        {
            await ReadExactChunkBytesAsync(input, prefix, cancellationToken).ConfigureAwait(false);
            int chunkPlaintextLength = ValidateChunkPrefix();
            await ReadExactChunkBytesAsync(
                input,
                ciphertextBuffer.AsMemory(0, chunkPlaintextLength),
                cancellationToken).ConfigureAwait(false);
            await ReadExactChunkBytesAsync(input, tag, cancellationToken).ConfigureAwait(false);
            DecryptChunk(chunkPlaintextLength);
            await CompleteOrAdvanceChunkAsync(cancellationToken).ConfigureAwait(false);
        }

        private int ValidateChunkPrefix()
        {
            uint plaintextLengthValue = BinaryPrimitives.ReadUInt32LittleEndian(prefix.AsSpan(0, sizeof(uint)));
            ushort chunkFlags = BinaryPrimitives.ReadUInt16LittleEndian(prefix.AsSpan(4, sizeof(ushort)));
            ushort reserved = BinaryPrimitives.ReadUInt16LittleEndian(prefix.AsSpan(6, sizeof(ushort)));
            bool isFinal = (chunkFlags & FileCrypterFormatConstants.ChunkFlagFinal) != 0;

            if ((chunkFlags & ~FileCrypterFormatConstants.KnownChunkFlags) != 0)
            {
                throw new FileCrypterFormatException(
                    FileCrypterFormatErrorCode.InvalidChunkFlags,
                    "The FileCrypter chunk contains unsupported flags.");
            }

            if (reserved != 0)
            {
                throw new FileCrypterFormatException(
                    FileCrypterFormatErrorCode.InvalidChunkReservedBytes,
                    "The FileCrypter chunk contains nonzero reserved bytes.");
            }

            if (plaintextLengthValue > header.ChunkSize || (isFinal && plaintextLengthValue == header.ChunkSize))
            {
                throw new FileCrypterFormatException(
                    FileCrypterFormatErrorCode.InvalidChunkLength,
                    "The FileCrypter chunk length is invalid.");
            }

            completed = isFinal;
            return checked((int)plaintextLengthValue);
        }

        private void DecryptChunk(int chunkPlaintextLength)
        {
            prefix.CopyTo(associatedData, FileCrypterFormatConstants.HeaderLength);
            BinaryPrimitives.WriteUInt32LittleEndian(
                nonce.AsSpan(FileCrypterFormatConstants.NoncePrefixLength, sizeof(uint)),
                chunkIndex);

            try
            {
                aesGcm.Decrypt(
                    nonce,
                    ciphertextBuffer.AsSpan(0, chunkPlaintextLength),
                    tag,
                    plaintextBuffer.AsSpan(0, chunkPlaintextLength),
                    associatedData);
            }
            catch (CryptographicException exception)
            {
                throw new FileCrypterFormatException(
                    FileCrypterFormatErrorCode.AuthenticationFailed,
                    "The FileCrypter payload could not be authenticated.",
                    exception);
            }

            inputBytes += prefix.Length + chunkPlaintextLength + tag.Length;
            outputBytes += chunkPlaintextLength;
            options.Progress?.Report(new FileCrypterProgress(inputBytes, outputBytes));

            plaintextOffset = 0;
            plaintextLength = chunkPlaintextLength;
        }

        private void CompleteOrAdvanceChunk()
        {
            if (completed)
            {
                EnsureNoTrailingData();
            }
            else
            {
                AdvanceChunkIndex();
            }
        }

        private async ValueTask CompleteOrAdvanceChunkAsync(CancellationToken cancellationToken)
        {
            if (completed)
            {
                await EnsureNoTrailingDataAsync(cancellationToken).ConfigureAwait(false);
            }
            else
            {
                AdvanceChunkIndex();
            }
        }

        private void AdvanceChunkIndex()
        {
            if (chunkIndex == uint.MaxValue)
            {
                throw new FileCrypterFormatException(
                    FileCrypterFormatErrorCode.TooManyChunks,
                    "The encrypted input contains too many chunks for the FileCrypter v1 chunk counter.");
            }

            chunkIndex++;
        }

        private void EnsureNoTrailingData()
        {
            int trailingBytesRead = input.Read(trailingByte);
            if (trailingBytesRead != 0)
            {
                throw new FileCrypterFormatException(
                    FileCrypterFormatErrorCode.TrailingData,
                    "The FileCrypter payload contains trailing data after the final chunk.");
            }
        }

        private async ValueTask EnsureNoTrailingDataAsync(CancellationToken cancellationToken)
        {
            int trailingBytesRead = await input.ReadAsync(trailingByte, cancellationToken).ConfigureAwait(false);
            if (trailingBytesRead != 0)
            {
                throw new FileCrypterFormatException(
                    FileCrypterFormatErrorCode.TrailingData,
                    "The FileCrypter payload contains trailing data after the final chunk.");
            }
        }
    }

    private static void ReadExactChunkBytes(Stream source, Span<byte> buffer)
    {
        int totalBytesRead = 0;

        while (totalBytesRead < buffer.Length)
        {
            int bytesRead = source.Read(buffer.Slice(totalBytesRead));
            if (bytesRead == 0)
            {
                break;
            }

            totalBytesRead += bytesRead;
        }

        if (totalBytesRead != buffer.Length)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.TruncatedChunk,
                "The FileCrypter chunk is truncated.");
        }
    }
}

using System.Buffers.Binary;
using System.Security.Cryptography;
using FileCrypter.Core.Cryptography;
using FileCrypter.Core.Format;

namespace FileCrypter.Core;

public static class FileCrypter
{
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
            options,
            overwrite,
            encrypt: false,
            cancellationToken);
    }

    public static async Task EncryptAsync(
        Stream plaintext,
        Stream encrypted,
        string password,
        FileCrypterOptions? options = null,
        CancellationToken cancellationToken = default)
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
        FileCrypterHeaderWriter.WritePasswordOnly(headerBytes, options, salt, noncePrefix);
        FileCrypterHeader header = FileCrypterHeaderParser.Parse(headerBytes);
        byte[] key = FileCrypterKeyDeriver.DerivePasswordOnlyKey(password, header);

        try
        {
            await encrypted.WriteAsync(headerBytes, cancellationToken).ConfigureAwait(false);

            using var aesGcm = new AesGcm(key, FileCrypterFormatConstants.AesGcmTagLength);
            byte[] plaintextBuffer = new byte[options.ChunkSize];
            byte[] ciphertextBuffer = new byte[options.ChunkSize];
            byte[] tag = new byte[FileCrypterFormatConstants.AesGcmTagLength];
            byte[] prefix = new byte[FileCrypterFormatConstants.ChunkFramePrefixLength];
            byte[] nonce = new byte[FileCrypterFormatConstants.AesGcmNonceLength];
            byte[] associatedData = new byte[FileCrypterFormatConstants.HeaderLength + FileCrypterFormatConstants.ChunkFramePrefixLength];

            headerBytes.CopyTo(associatedData, 0);
            header.NoncePrefix.CopyTo(nonce);

            uint chunkIndex = 0;
            long inputBytes = 0;
            long outputBytes = headerBytes.Length;

            while (true)
            {
                int plaintextLength = await ReadChunkAsync(
                    plaintext,
                    plaintextBuffer.AsMemory(0, options.ChunkSize),
                    cancellationToken).ConfigureAwait(false);

                bool isFinal = plaintextLength < options.ChunkSize;
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

                await encrypted.WriteAsync(prefix, cancellationToken).ConfigureAwait(false);
                await encrypted.WriteAsync(ciphertextBuffer.AsMemory(0, plaintextLength), cancellationToken).ConfigureAwait(false);
                await encrypted.WriteAsync(tag, cancellationToken).ConfigureAwait(false);

                inputBytes += plaintextLength;
                outputBytes += prefix.Length + plaintextLength + tag.Length;
                options.Progress?.Report(new FileCrypterProgress(inputBytes, outputBytes));

                if (isFinal)
                {
                    break;
                }

                if (chunkIndex == uint.MaxValue)
                {
                    throw new FileCrypterFormatException(
                        FileCrypterFormatErrorCode.TooManyChunks,
                        "The input is too large for the FileCrypter v1 chunk counter.");
                }

                chunkIndex++;
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
        ValidateSupportedPayload(header);

        byte[] key = FileCrypterKeyDeriver.DerivePasswordOnlyKey(password, header);

        try
        {
            using var aesGcm = new AesGcm(key, FileCrypterFormatConstants.AesGcmTagLength);
            byte[] prefix = new byte[FileCrypterFormatConstants.ChunkFramePrefixLength];
            byte[] nonce = new byte[FileCrypterFormatConstants.AesGcmNonceLength];
            byte[] associatedData = new byte[FileCrypterFormatConstants.HeaderLength + FileCrypterFormatConstants.ChunkFramePrefixLength];
            byte[] trailingByte = new byte[1];
            int chunkSize = checked((int)header.ChunkSize);
            byte[] ciphertextBuffer = new byte[chunkSize];
            byte[] plaintextBuffer = new byte[chunkSize];
            byte[] tag = new byte[FileCrypterFormatConstants.AesGcmTagLength];

            headerBytes.CopyTo(associatedData, 0);
            header.NoncePrefix.CopyTo(nonce);

            uint chunkIndex = 0;
            long inputBytes = headerBytes.Length;
            long outputBytes = 0;

            while (true)
            {
                await ReadExactChunkBytesAsync(encrypted, prefix, cancellationToken).ConfigureAwait(false);

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

                int plaintextLength = checked((int)plaintextLengthValue);
                await ReadExactChunkBytesAsync(
                    encrypted,
                    ciphertextBuffer.AsMemory(0, plaintextLength),
                    cancellationToken).ConfigureAwait(false);
                await ReadExactChunkBytesAsync(encrypted, tag, cancellationToken).ConfigureAwait(false);

                prefix.CopyTo(associatedData, FileCrypterFormatConstants.HeaderLength);
                BinaryPrimitives.WriteUInt32LittleEndian(
                    nonce.AsSpan(FileCrypterFormatConstants.NoncePrefixLength, sizeof(uint)),
                    chunkIndex);

                try
                {
                    aesGcm.Decrypt(
                        nonce,
                        ciphertextBuffer.AsSpan(0, plaintextLength),
                        tag,
                        plaintextBuffer.AsSpan(0, plaintextLength),
                        associatedData);
                }
                catch (CryptographicException exception)
                {
                    throw new FileCrypterFormatException(
                        FileCrypterFormatErrorCode.AuthenticationFailed,
                        "The FileCrypter payload could not be authenticated.",
                        exception);
                }

                await plaintext.WriteAsync(
                    plaintextBuffer.AsMemory(0, plaintextLength),
                    cancellationToken).ConfigureAwait(false);

                inputBytes += prefix.Length + plaintextLength + tag.Length;
                outputBytes += plaintextLength;
                options.Progress?.Report(new FileCrypterProgress(inputBytes, outputBytes));

                if (isFinal)
                {
                    int trailingBytesRead = await encrypted.ReadAsync(trailingByte, cancellationToken).ConfigureAwait(false);
                    if (trailingBytesRead != 0)
                    {
                        throw new FileCrypterFormatException(
                            FileCrypterFormatErrorCode.TrailingData,
                            "The FileCrypter payload contains trailing data after the final chunk.");
                    }

                    break;
                }

                if (chunkIndex == uint.MaxValue)
                {
                    throw new FileCrypterFormatException(
                        FileCrypterFormatErrorCode.TooManyChunks,
                        "The encrypted input contains too many chunks for the FileCrypter v1 chunk counter.");
                }

                chunkIndex++;
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

        if (!overwrite)
        {
            fullOutputPath = GetAvailableOutputPath(fullOutputPath);
        }

        if (PathsEqual(fullInputPath, fullOutputPath))
        {
            throw new ArgumentException("The input and output paths must be different.", nameof(outputPath));
        }

        string stagingPath = CreateStagingPath(fullOutputPath);
        bool completed = false;

        try
        {
            await using FileStream input = new(
                fullInputPath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                bufferSize: 81920,
                FileOptions.SequentialScan);

            await using (FileStream output = new(
                stagingPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                bufferSize: 81920,
                FileOptions.SequentialScan))
            {
                if (encrypt)
                {
                    await EncryptAsync(input, output, password, options, cancellationToken).ConfigureAwait(false);
                }
                else
                {
                    await DecryptAsync(input, output, password, options, cancellationToken).ConfigureAwait(false);
                }
            }

            File.Move(stagingPath, fullOutputPath, overwrite);
            completed = true;
            return fullOutputPath;
        }
        finally
        {
            if (!completed)
            {
                TryDeleteFile(stagingPath);
            }
        }
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
        if (!File.Exists(fullOutputPath) && !Directory.Exists(fullOutputPath))
        {
            return fullOutputPath;
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
            if (!File.Exists(candidatePath) && !Directory.Exists(candidatePath))
            {
                return candidatePath;
            }
        }

        throw new IOException("No available auto-renamed output path could be found.");
    }

    private static bool PathsEqual(string left, string right)
    {
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(left, right, comparison);
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

    private static void ValidateSupportedPayload(FileCrypterHeader header)
    {
        if (header.IsKeyFileRequired)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedKeyFileRequirement,
                "This FileCrypter operation does not support key-file encrypted payloads yet.");
        }

        if (header.CompressionAlgorithmId != FileCrypterFormatConstants.CompressionNone)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedCompressionAlgorithm,
                "This FileCrypter operation does not support compressed payloads yet.");
        }

        if (header.PayloadKind != FileCrypterFormatConstants.PayloadKindSingleFile)
        {
            throw new FileCrypterFormatException(
                FileCrypterFormatErrorCode.UnsupportedPayloadKind,
                "This FileCrypter operation only supports single-file payloads.");
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
}

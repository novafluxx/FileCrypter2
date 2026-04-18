using System.Globalization;
using FileCrypter.Core;
using FileCrypter.Core.Format;

internal sealed class FileCrypterCommand
{
    private const string DefaultEncryptedSuffix = ".encrypted";

    private readonly IFileCrypterConsole console;
    private readonly FileCrypterOptions? options;

    public FileCrypterCommand(IFileCrypterConsole console, FileCrypterOptions? options = null)
    {
        this.console = console;
        this.options = options;
    }

    public async Task<int> RunAsync(string[] args)
    {
        if (args.Length == 0 || args[0] is "-h" or "--help")
        {
            WriteUsage();
            return 0;
        }

        try
        {
            return args[0] switch
            {
                "encrypt" => await RunTransformAsync(args, encrypt: true).ConfigureAwait(false),
                "decrypt" => await RunTransformAsync(args, encrypt: false).ConfigureAwait(false),
                _ => WriteError("Unknown command. Use 'encrypt' or 'decrypt'."),
            };
        }
        catch (FileCrypterFormatException exception)
        {
            return WriteFormatError(exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return WritePathError(exception.Message);
        }
        catch (ArgumentException exception)
        {
            return WritePathError(exception.Message);
        }
    }

    private async Task<int> RunTransformAsync(string[] args, bool encrypt)
    {
        if (args.Length < 2)
        {
            return WriteError("Missing input path.");
        }

        string inputPath = args[1];
        string? outputPath = null;
        string? password = null;
        string? keyFilePath = null;
        string? generateKeyFilePath = null;
        bool overwrite = false;
        bool compress = false;

        for (int index = 2; index < args.Length; index++)
        {
            string arg = args[index];
            switch (arg)
            {
                case "--password":
                    if (++index >= args.Length)
                    {
                        return WriteError("Missing value for --password.");
                    }

                    password = args[index];
                    break;

                case "--password-stdin":
                    password = await console.In.ReadLineAsync().ConfigureAwait(false);
                    break;

                case "--key-file":
                    if (++index >= args.Length)
                    {
                        return WriteError("Missing value for --key-file.");
                    }

                    keyFilePath = args[index];
                    break;

                case "--generate-key-file":
                    if (!encrypt)
                    {
                        return WriteError("--generate-key-file is only supported with encrypt.");
                    }

                    if (++index >= args.Length)
                    {
                        return WriteError("Missing value for --generate-key-file.");
                    }

                    generateKeyFilePath = args[index];
                    break;

                case "--overwrite":
                    overwrite = true;
                    break;

                case "--compress":
                    if (!encrypt)
                    {
                        return WriteError("--compress is only supported with encrypt.");
                    }

                    compress = true;
                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        return WriteError($"Unknown option '{arg}'.");
                    }

                    if (outputPath is not null)
                    {
                        return WriteError("Only one output path may be specified.");
                    }

                    outputPath = arg;
                    break;
            }
        }

        if (keyFilePath is not null && generateKeyFilePath is not null)
        {
            return WriteError("Use either --key-file or --generate-key-file, not both.");
        }

        if (password is null)
        {
            password = ReadPasswordFromInteractiveConsole();
        }

        if (string.IsNullOrEmpty(password))
        {
            return WriteError("A password is required. Use --password, --password-stdin, or run from an interactive terminal.");
        }

        if (!File.Exists(inputPath))
        {
            return WriteError($"Input file does not exist: {inputPath}");
        }

        outputPath ??= encrypt ? inputPath + DefaultEncryptedSuffix : GetDefaultDecryptedPath(inputPath);
        if (encrypt && generateKeyFilePath is not null)
        {
            if (PathsEqual(Path.GetFullPath(inputPath), Path.GetFullPath(generateKeyFilePath)))
            {
                return WritePathError("The input and generated key file paths must be different.");
            }

            if (PathsEqual(Path.GetFullPath(outputPath), Path.GetFullPath(generateKeyFilePath)))
            {
                return WritePathError("The output and generated key file paths must be different.");
            }
        }

        long inputLength = new FileInfo(inputPath).Length;
        (FileCrypterOptions transformOptions, CliProgressReporter progressReporter) =
            CreateTransformOptionsWithProgress(inputLength, encrypt, compress);
        string finalOutputPath;
        string? transformKeyFilePath = keyFilePath;

        if (encrypt && generateKeyFilePath is not null)
        {
            transformKeyFilePath = await FileCrypter.Core.FileCrypter.GenerateKeyFileAsync(
                generateKeyFilePath,
                options).ConfigureAwait(false);
            console.Error.WriteLine($"Generated key file: {transformKeyFilePath}");
            console.Error.WriteLine("Keep this key file unchanged; losing it makes the encrypted file unrecoverable.");
        }

        if (encrypt)
        {
            finalOutputPath = transformKeyFilePath is null
                ? await FileCrypter.Core.FileCrypter.EncryptFileAsync(
                    inputPath,
                    outputPath,
                    password,
                    transformOptions,
                    overwrite).ConfigureAwait(false)
                : await FileCrypter.Core.FileCrypter.EncryptFileAsync(
                    inputPath,
                    outputPath,
                    password,
                    transformKeyFilePath,
                    transformOptions,
                    overwrite).ConfigureAwait(false);
        }
        else
        {
            finalOutputPath = transformKeyFilePath is null
                ? await FileCrypter.Core.FileCrypter.DecryptFileAsync(
                    inputPath,
                    outputPath,
                    password,
                    transformOptions,
                    overwrite).ConfigureAwait(false)
                : await FileCrypter.Core.FileCrypter.DecryptFileAsync(
                    inputPath,
                    outputPath,
                    password,
                    transformKeyFilePath,
                    transformOptions,
                    overwrite).ConfigureAwait(false);
        }

        progressReporter.ReportComplete();
        console.Out.WriteLine(finalOutputPath);
        return 0;
    }

    private string? ReadPasswordFromInteractiveConsole()
    {
        if (console.IsInputRedirected)
        {
            return null;
        }

        console.Error.Write("Password: ");
        var password = new List<char>();

        while (true)
        {
            ConsoleKeyInfo key = console.ReadKey(intercept: true);

            if (key.Key is ConsoleKey.Enter)
            {
                console.Error.WriteLine();
                return new string(password.ToArray());
            }

            if (key.Key is ConsoleKey.Backspace)
            {
                if (password.Count > 0)
                {
                    password.RemoveAt(password.Count - 1);
                }

                continue;
            }

            if (!char.IsControl(key.KeyChar))
            {
                password.Add(key.KeyChar);
            }
        }
    }

    private static string GetDefaultDecryptedPath(string inputPath)
    {
        return inputPath.EndsWith(DefaultEncryptedSuffix, StringComparison.OrdinalIgnoreCase)
            ? inputPath[..^DefaultEncryptedSuffix.Length]
            : inputPath + ".decrypted";
    }

    private static bool PathsEqual(string left, string right)
    {
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(left, right, comparison);
    }

    private (FileCrypterOptions Options, CliProgressReporter Reporter) CreateTransformOptionsWithProgress(
        long inputLength,
        bool encrypt,
        bool compress)
    {
        FileCrypterOptions sourceOptions = options ?? new FileCrypterOptions();
        var progressReporter = new CliProgressReporter(
            console.Error,
            encrypt ? "Encrypting" : "Decrypting",
            inputLength,
            sourceOptions.Progress);

        var transformOptions = new FileCrypterOptions
        {
            ChunkSize = sourceOptions.ChunkSize,
            Argon2MemoryKiB = sourceOptions.Argon2MemoryKiB,
            Argon2Iterations = sourceOptions.Argon2Iterations,
            Argon2Parallelism = sourceOptions.Argon2Parallelism,
            EnableCompression = sourceOptions.EnableCompression || compress,
            Progress = progressReporter,
        };

        return (transformOptions, progressReporter);
    }

    private int WriteError(string message)
    {
        console.Error.WriteLine(message);
        return 1;
    }

    private int WriteFormatError(FileCrypterFormatException exception)
    {
        console.Error.WriteLine($"{exception.Message} ({exception.Code})");
        string? hint = exception.Code switch
        {
            FileCrypterFormatErrorCode.AuthenticationFailed =>
                "Check the password and key file, then try again.",
            FileCrypterFormatErrorCode.KeyFileRequired =>
                "Provide the matching key file with --key-file.",
            FileCrypterFormatErrorCode.InvalidMagic =>
                "Choose a FileCrypter .encrypted file produced by this app.",
            FileCrypterFormatErrorCode.TruncatedHeader or FileCrypterFormatErrorCode.TruncatedChunk =>
                "The encrypted file appears incomplete or damaged. Try a fresh copy of the file.",
            FileCrypterFormatErrorCode.InvalidCompressedPayload =>
                "The compressed payload appears damaged. Try a fresh copy of the encrypted file.",
            FileCrypterFormatErrorCode.UnsupportedVersion or
            FileCrypterFormatErrorCode.UnsupportedCompressionAlgorithm or
            FileCrypterFormatErrorCode.UnsupportedPayloadKind or
            FileCrypterFormatErrorCode.UnsupportedKeyFileRequirement =>
                "This FileCrypter build cannot open that payload yet.",
            _ => "The encrypted file metadata or payload is not valid for this FileCrypter version.",
        };

        console.Error.WriteLine(hint);
        return 1;
    }

    private int WritePathError(string message)
    {
        console.Error.WriteLine($"Path error: {message}");
        console.Error.WriteLine("Check that the input and output paths are files you can access, and that the output directory already exists.");
        return 1;
    }

    private void WriteUsage()
    {
        console.Out.WriteLine(
            """
            Usage:
              filecrypter encrypt <input> [output] [--password <password> | --password-stdin] [--key-file <path> | --generate-key-file <path>] [--compress] [--overwrite]
              filecrypter decrypt <input> [output] [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]

            If no password option is supplied, FileCrypter prompts without echoing the password when run interactively.
            If output is omitted, encryption appends .encrypted and decryption removes .encrypted when present.
            Use --compress during encryption to reduce compatible payloads before encryption. Decryption detects compressed files automatically.
            Use --key-file with an existing key file for password plus key-file protection. The same key file is required
            for decryption; lost or changed key files cannot be recovered. Existing key files may be up to 16 MiB.
            Use --generate-key-file during encryption to create a new 32-byte key file before encrypting. Keep the generated
            key file unchanged; losing it makes the encrypted file unrecoverable.
            """);
    }

    private sealed class CliProgressReporter : IProgress<FileCrypterProgress>
    {
        private readonly TextWriter writer;
        private readonly string label;
        private readonly long totalInputBytes;
        private readonly IProgress<FileCrypterProgress>? innerProgress;
        private int lastPercent = -1;

        public CliProgressReporter(
            TextWriter writer,
            string label,
            long totalInputBytes,
            IProgress<FileCrypterProgress>? innerProgress)
        {
            this.writer = writer;
            this.label = label;
            this.totalInputBytes = totalInputBytes;
            this.innerProgress = innerProgress;
        }

        public void Report(FileCrypterProgress value)
        {
            innerProgress?.Report(value);

            long processedInputBytes = totalInputBytes <= 0
                ? 0
                : Math.Min(value.InputBytes, totalInputBytes);
            int percent = totalInputBytes <= 0
                ? 100
                : (int)Math.Min(100, processedInputBytes * 100 / totalInputBytes);

            if (percent == lastPercent)
            {
                return;
            }

            lastPercent = percent;
            writer.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{label}: {percent}% ({processedInputBytes}/{totalInputBytes} bytes)"));
        }

        public void ReportComplete()
        {
            if (lastPercent == 100)
            {
                return;
            }

            lastPercent = 100;
            writer.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{label}: 100% ({totalInputBytes}/{totalInputBytes} bytes)"));
        }
    }
}

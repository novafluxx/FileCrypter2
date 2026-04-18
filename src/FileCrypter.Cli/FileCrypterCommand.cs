using System.Globalization;
using FileCrypter.Core;
using FileCrypter.Core.Format;

internal sealed class FileCrypterCommand
{
    private const string DefaultEncryptedSuffix = ".encrypted";
    private const string DefaultArchiveEncryptedSuffix = ".tar.zst.encrypted";
    private const string GeneratedArchiveNamePrefix = "filecrypter-archive-";
    private const string ArchiveTimestampFormat = "yyyyMMdd-HHmmss";
    private const string CrossPlatformInvalidArchiveNameCharacters = "<>:\"/\\|?*";

    private readonly IFileCrypterConsole console;
    private readonly FileCrypterOptions? options;
    private readonly FileCrypterSettingsStore settingsStore;

    public FileCrypterCommand(
        IFileCrypterConsole console,
        FileCrypterOptions? options = null,
        FileCrypterSettingsStore? settingsStore = null)
    {
        this.console = console;
        this.options = options;
        this.settingsStore = settingsStore ?? new FileCrypterSettingsStore();
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
                "batch-encrypt" => await RunBatchAsync(args, encrypt: true).ConfigureAwait(false),
                "batch-decrypt" => await RunBatchAsync(args, encrypt: false).ConfigureAwait(false),
                "archive-encrypt" => await RunArchiveEncryptAsync(args).ConfigureAwait(false),
                "archive-decrypt" => await RunArchiveDecryptAsync(args).ConfigureAwait(false),
                "settings" => await RunSettingsAsync(args).ConfigureAwait(false),
                _ => WriteError("Unknown command. Use 'encrypt', 'decrypt', 'batch-encrypt', 'batch-decrypt', 'archive-encrypt', 'archive-decrypt', or 'settings'."),
            };
        }
        catch (FileCrypterFormatException exception)
        {
            return WriteFormatError(exception);
        }
        catch (InvalidDataException exception)
        {
            return WriteSettingsError(exception.Message);
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
        FileCrypterSettings settings = encrypt
            ? await settingsStore.LoadAsync().ConfigureAwait(false)
            : new FileCrypterSettings();
        (FileCrypterOptions transformOptions, CliProgressReporter progressReporter) =
            CreateTransformOptionsWithProgress(inputLength, encrypt, compress, settings);
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

    private async Task<int> RunBatchAsync(string[] args, bool encrypt)
    {
        if (args.Length < 2)
        {
            return WriteError("Missing output directory.");
        }

        string outputDirectory = args[1];
        string? password = null;
        string? keyFilePath = null;
        bool overwrite = false;
        var inputPaths = new List<string>();

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

                case "--overwrite":
                    overwrite = true;
                    break;

                case "--compress":
                    if (!encrypt)
                    {
                        return WriteError("--compress is only supported with batch-encrypt.");
                    }

                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        return WriteError($"Unknown option '{arg}'.");
                    }

                    inputPaths.Add(arg);
                    break;
            }
        }

        if (inputPaths.Count == 0)
        {
            return WriteError("At least one input path is required.");
        }

        if (password is null)
        {
            password = ReadPasswordFromInteractiveConsole();
        }

        if (string.IsNullOrEmpty(password))
        {
            return WriteError("A password is required. Use --password, --password-stdin, or run from an interactive terminal.");
        }

        FileCrypterBatchResult result = encrypt
            ? keyFilePath is null
                ? await FileCrypter.Core.FileCrypter.EncryptFilesAsync(
                    inputPaths,
                    outputDirectory,
                    password,
                    options,
                    overwrite).ConfigureAwait(false)
                : await FileCrypter.Core.FileCrypter.EncryptFilesAsync(
                    inputPaths,
                    outputDirectory,
                    password,
                    keyFilePath,
                    options,
                    overwrite).ConfigureAwait(false)
            : keyFilePath is null
                ? await FileCrypter.Core.FileCrypter.DecryptFilesAsync(
                    inputPaths,
                    outputDirectory,
                    password,
                    options,
                    overwrite).ConfigureAwait(false)
                : await FileCrypter.Core.FileCrypter.DecryptFilesAsync(
                    inputPaths,
                    outputDirectory,
                    password,
                    keyFilePath,
                    options,
                    overwrite).ConfigureAwait(false);

        foreach (FileCrypterBatchItemResult item in result.Items)
        {
            if (item.Succeeded)
            {
                console.Out.WriteLine(item.OutputPath);
            }
            else
            {
                WriteBatchItemError(item);
            }
        }

        console.Error.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Batch complete: {result.SucceededCount}/{result.Items.Count} succeeded."));
        return result.Succeeded ? 0 : 1;
    }

    private async Task<int> RunArchiveEncryptAsync(string[] args)
    {
        if (args.Length < 2)
        {
            return WriteError("Missing output archive path.");
        }

        string outputArchivePath = args[1];
        string? archiveName = null;
        string? password = null;
        string? keyFilePath = null;
        bool overwrite = false;
        var inputPaths = new List<string>();

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

                case "--archive-name":
                    if (++index >= args.Length)
                    {
                        return WriteError("Missing value for --archive-name.");
                    }

                    archiveName = args[index];
                    break;

                case "--overwrite":
                    overwrite = true;
                    break;

                case "--compress":
                    break;

                default:
                    if (arg.StartsWith("-", StringComparison.Ordinal))
                    {
                        return WriteError($"Unknown option '{arg}'.");
                    }

                    inputPaths.Add(arg);
                    break;
            }
        }

        if (inputPaths.Count == 0)
        {
            return WriteError("At least one input path is required.");
        }

        if (password is null)
        {
            password = ReadPasswordFromInteractiveConsole();
        }

        if (string.IsNullOrEmpty(password))
        {
            return WriteError("A password is required. Use --password, --password-stdin, or run from an interactive terminal.");
        }

        if (Directory.Exists(outputArchivePath))
        {
            if (!TryCreateArchiveFileName(archiveName, out string archiveFileName, out string? archiveNameError))
            {
                return WriteError(archiveNameError ?? "Archive name is invalid.");
            }

            outputArchivePath = Path.Combine(outputArchivePath, archiveFileName);
        }
        else if (archiveName is not null)
        {
            return WriteError("--archive-name can only be used when the output archive path is an existing directory.");
        }

        FileCrypterOptions archiveOptions = CreateArchiveOptionsWithProgress();

        string finalOutputPath = keyFilePath is null
            ? await FileCrypter.Core.FileCrypter.EncryptArchiveAsync(
                inputPaths,
                outputArchivePath,
                password,
                archiveOptions,
                overwrite).ConfigureAwait(false)
            : await FileCrypter.Core.FileCrypter.EncryptArchiveAsync(
                inputPaths,
                outputArchivePath,
                password,
                keyFilePath,
                archiveOptions,
                overwrite).ConfigureAwait(false);

        console.Out.WriteLine(finalOutputPath);
        return 0;
    }

    private async Task<int> RunArchiveDecryptAsync(string[] args)
    {
        if (args.Length < 2)
        {
            return WriteError("Missing input archive path.");
        }

        if (args.Length < 3)
        {
            return WriteError("Missing output directory.");
        }

        string inputArchivePath = args[1];
        string outputDirectory = args[2];
        string? password = null;
        string? keyFilePath = null;
        bool overwrite = false;

        for (int index = 3; index < args.Length; index++)
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

                case "--overwrite":
                    overwrite = true;
                    break;

                default:
                    return arg.StartsWith("-", StringComparison.Ordinal)
                        ? WriteError($"Unknown option '{arg}'.")
                        : WriteError("archive-decrypt accepts exactly one input archive and one output directory.");
            }
        }

        if (password is null)
        {
            password = ReadPasswordFromInteractiveConsole();
        }

        if (string.IsNullOrEmpty(password))
        {
            return WriteError("A password is required. Use --password, --password-stdin, or run from an interactive terminal.");
        }

        FileCrypterOptions archiveOptions = CreateArchiveOptionsWithProgress();

        IReadOnlyList<string> extractedPaths = keyFilePath is null
            ? await FileCrypter.Core.FileCrypter.DecryptArchiveAsync(
                inputArchivePath,
                outputDirectory,
                password,
                archiveOptions,
                overwrite).ConfigureAwait(false)
            : await FileCrypter.Core.FileCrypter.DecryptArchiveAsync(
                inputArchivePath,
                outputDirectory,
                password,
                keyFilePath,
                archiveOptions,
                overwrite).ConfigureAwait(false);

        foreach (string extractedPath in extractedPaths)
        {
            console.Out.WriteLine(extractedPath);
        }

        return 0;
    }

    private async Task<int> RunSettingsAsync(string[] args)
    {
        if (args.Length == 1 || args[1] == "show")
        {
            if (args.Length > 2)
            {
                return WriteError("settings show does not accept extra arguments.");
            }

            FileCrypterSettings settings = await settingsStore.LoadAsync().ConfigureAwait(false);
            console.Out.WriteLine($"Compression default: {FormatOnOff(settings.EnableCompressionByDefault)}");
            console.Out.WriteLine($"Settings file: {settingsStore.SettingsPath}");
            return 0;
        }

        if (args[1] == "set")
        {
            if (args.Length != 4 || args[2] != "compression-default")
            {
                return WriteError("Usage: filecrypter settings set compression-default <on|off>");
            }

            if (!TryParseOnOff(args[3], out bool enableCompressionByDefault))
            {
                return WriteError("Compression default must be 'on' or 'off'.");
            }

            var settings = new FileCrypterSettings
            {
                EnableCompressionByDefault = enableCompressionByDefault,
            };
            await settingsStore.SaveAsync(settings).ConfigureAwait(false);
            console.Out.WriteLine($"Compression default: {FormatOnOff(settings.EnableCompressionByDefault)}");
            return 0;
        }

        return WriteError("Unknown settings command. Use 'settings show' or 'settings set compression-default <on|off>'.");
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
        bool compress,
        FileCrypterSettings settings)
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
            EnableCompression = sourceOptions.EnableCompression ||
                compress ||
                (encrypt && settings.EnableCompressionByDefault),
            Progress = progressReporter,
        };

        return (transformOptions, progressReporter);
    }

    private FileCrypterOptions CreateArchiveOptionsWithProgress()
    {
        FileCrypterOptions sourceOptions = options ?? new FileCrypterOptions();
        var progressReporter = new CliProgressReporter(
            console.Error,
            "Archive",
            totalInputBytes: 0,
            sourceOptions.Progress);

        var archiveOptions = new FileCrypterOptions
        {
            ChunkSize = sourceOptions.ChunkSize,
            Argon2MemoryKiB = sourceOptions.Argon2MemoryKiB,
            Argon2Iterations = sourceOptions.Argon2Iterations,
            Argon2Parallelism = sourceOptions.Argon2Parallelism,
            EnableCompression = sourceOptions.EnableCompression,
            Progress = progressReporter,
        };

        return archiveOptions;
    }

    private static bool TryParseOnOff(string value, out bool parsed)
    {
        if (value.Equals("on", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("true", StringComparison.OrdinalIgnoreCase))
        {
            parsed = true;
            return true;
        }

        if (value.Equals("off", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            parsed = false;
            return true;
        }

        parsed = false;
        return false;
    }

    private static string FormatOnOff(bool value)
    {
        return value ? "on" : "off";
    }

    private static bool TryCreateArchiveFileName(
        string? archiveName,
        out string archiveFileName,
        out string? error)
    {
        if (archiveName is null)
        {
            archiveFileName = GeneratedArchiveNamePrefix +
                DateTimeOffset.Now.ToString(ArchiveTimestampFormat, CultureInfo.InvariantCulture) +
                DefaultArchiveEncryptedSuffix;
            error = null;
            return true;
        }

        string trimmedArchiveName = archiveName.Trim();
        if (string.IsNullOrEmpty(trimmedArchiveName))
        {
            archiveFileName = string.Empty;
            error = "Archive name cannot be blank.";
            return false;
        }

        string archiveBaseName = trimmedArchiveName.EndsWith(DefaultArchiveEncryptedSuffix, StringComparison.OrdinalIgnoreCase)
            ? trimmedArchiveName[..^DefaultArchiveEncryptedSuffix.Length]
            : trimmedArchiveName;
        if (string.IsNullOrWhiteSpace(archiveBaseName))
        {
            archiveFileName = string.Empty;
            error = "Archive name must include a file name before .tar.zst.encrypted.";
            return false;
        }

        if (!IsSafeArchiveFileName(trimmedArchiveName))
        {
            archiveFileName = string.Empty;
            error = "Archive name contains characters or reserved words that are unsafe in file names.";
            return false;
        }

        archiveFileName = archiveBaseName + DefaultArchiveEncryptedSuffix;
        error = null;
        return true;
    }

    private static bool IsSafeArchiveFileName(string fileName)
    {
        if (fileName is "." or ".." ||
            fileName.EndsWith(' ') ||
            fileName.EndsWith('.'))
        {
            return false;
        }

        if (fileName.Any(character =>
            char.IsControl(character) ||
            CrossPlatformInvalidArchiveNameCharacters.Contains(character)))
        {
            return false;
        }

        string firstNamePart = fileName.Split('.')[0].TrimEnd(' ');
        return !IsReservedWindowsFileName(firstNamePart);
    }

    private static bool IsReservedWindowsFileName(string fileName)
    {
        return fileName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            IsReservedWindowsPortName(fileName, "COM") ||
            IsReservedWindowsPortName(fileName, "LPT");
    }

    private static bool IsReservedWindowsPortName(string fileName, string prefix)
    {
        return fileName.Length == 4 &&
            fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            fileName[3] is >= '1' and <= '9';
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
            FileCrypterFormatErrorCode.InvalidArchivePayload =>
                "The archive payload appears damaged or unsafe. Try a fresh copy of the encrypted archive.",
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

    private int WriteSettingsError(string message)
    {
        console.Error.WriteLine($"Settings error: {message}");
        console.Error.WriteLine("Run 'filecrypter settings set compression-default on' or 'off' to recreate the settings file.");
        return 1;
    }

    private void WriteBatchItemError(FileCrypterBatchItemResult item)
    {
        Exception exception = item.Error ?? new InvalidOperationException("The batch item failed without an error.");
        string message = exception is FileCrypterFormatException formatException
            ? $"{formatException.Message} ({formatException.Code})"
            : exception.Message;
        console.Error.WriteLine($"Failed: {item.InputPath}");
        console.Error.WriteLine(message);
    }

    private void WriteUsage()
    {
        console.Out.WriteLine(
            """
            Usage:
              filecrypter encrypt <input> [output] [--password <password> | --password-stdin] [--key-file <path> | --generate-key-file <path>] [--compress] [--overwrite]
              filecrypter decrypt <input> [output] [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]
              filecrypter batch-encrypt <output-directory> <input>... [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]
              filecrypter batch-decrypt <output-directory> <input>... [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]
              filecrypter archive-encrypt <output-archive-or-directory> <input>... [--archive-name <name>] [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]
              filecrypter archive-decrypt <input-archive> <output-directory> [--password <password> | --password-stdin] [--key-file <path>] [--overwrite]
              filecrypter settings show
              filecrypter settings set compression-default <on|off>

            If no password option is supplied, FileCrypter prompts without echoing the password when run interactively.
            If output is omitted, encryption appends .encrypted and decryption removes .encrypted when present.
            Use --compress during encryption to reduce compatible payloads before encryption. Decryption detects compressed files automatically.
            Set compression-default on to compress single-file encryption by default.
            Batch encryption compresses each file automatically and writes one output path per successful file to stdout.
            Archive encryption writes one compressed tar archive payload and archive decryption writes each extracted path to stdout.
            If archive-encrypt receives an output directory, it creates a timestamped .tar.zst.encrypted archive there.
            Use --archive-name with an output directory to choose a safe custom archive basename.
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
        private string? lastLabel;
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

            string progressLabel = GetProgressLabel(value.Phase, label);
            long currentTotalInputBytes = value.TotalInputBytes ?? totalInputBytes;
            if (!string.Equals(progressLabel, lastLabel, StringComparison.Ordinal))
            {
                lastLabel = progressLabel;
                lastPercent = -1;
            }

            long processedInputBytes = currentTotalInputBytes <= 0
                ? 0
                : Math.Min(value.InputBytes, currentTotalInputBytes);
            int percent = currentTotalInputBytes <= 0
                ? 100
                : (int)Math.Min(100, processedInputBytes * 100 / currentTotalInputBytes);

            if (percent == lastPercent)
            {
                return;
            }

            lastPercent = percent;
            writer.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{progressLabel}: {percent}% ({processedInputBytes}/{currentTotalInputBytes} bytes)"));
        }

        public void ReportComplete()
        {
            if (lastPercent == 100)
            {
                return;
            }

            lastPercent = 100;
            lastLabel = label;
            writer.WriteLine(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"{label}: 100% ({totalInputBytes}/{totalInputBytes} bytes)"));
        }

        private static string GetProgressLabel(string? phase, string fallbackLabel)
        {
            return phase switch
            {
                FileCrypterProgressPhases.CreatingArchive => "Creating archive",
                FileCrypterProgressPhases.EncryptingArchive => "Encrypting archive",
                FileCrypterProgressPhases.DecryptingArchive => "Decrypting archive",
                FileCrypterProgressPhases.ExtractingArchive => "Extracting archive",
                _ => fallbackLabel,
            };
        }
    }
}

using System.Globalization;
using FileCrypter.Core;
using FileCrypter.Core.Format;
using FileCrypter.Core.Settings;

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
                "inspect" => await RunInspectAsync(args).ConfigureAwait(false),
                "verify" => await RunVerifyAsync(args).ConfigureAwait(false),
                "batch-encrypt" => await RunBatchAsync(args, encrypt: true).ConfigureAwait(false),
                "batch-decrypt" => await RunBatchAsync(args, encrypt: false).ConfigureAwait(false),
                "archive-encrypt" => await RunArchiveEncryptAsync(args).ConfigureAwait(false),
                "archive-decrypt" => await RunArchiveDecryptAsync(args).ConfigureAwait(false),
                "settings" => await RunSettingsAsync(args).ConfigureAwait(false),
                _ => WriteError("Unknown command. Use 'encrypt', 'decrypt', 'inspect', 'verify', 'batch-encrypt', 'batch-decrypt', 'archive-encrypt', 'archive-decrypt', or 'settings'."),
            };
        }
        catch (FileCrypterFormatException exception)
        {
            return WriteFormatError(args[0], exception);
        }
        catch (InvalidDataException)
        {
            return WriteSettingsError();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return WritePathError(exception);
        }
        catch (ArgumentException exception)
        {
            return WritePathError(exception);
        }
        catch (Exception exception) when (exception is InvalidOperationException or NotSupportedException or PlatformNotSupportedException)
        {
            return WriteOperationError();
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
                    WritePasswordArgumentWarning();
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
            return WritePathError("The input file does not exist.");
        }

        ThrowIfContainsParentSegment(Path.GetFullPath(inputPath));

        if (keyFilePath is not null)
        {
            int keyFileValidationResult = ValidateExistingKeyFilePath(keyFilePath);
            if (keyFileValidationResult != 0)
            {
                return keyFileValidationResult;
            }
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
                    WritePasswordArgumentWarning();
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

        if (keyFilePath is not null)
        {
            int keyFileValidationResult = ValidateExistingKeyFilePath(keyFilePath);
            if (keyFileValidationResult != 0)
            {
                return keyFileValidationResult;
            }
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
                WriteBatchItemError(encrypt ? "batch-encrypt" : "batch-decrypt", item);
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
                    WritePasswordArgumentWarning();
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

        if (keyFilePath is not null)
        {
            int keyFileValidationResult = ValidateExistingKeyFilePath(keyFilePath);
            if (keyFileValidationResult != 0)
            {
                return keyFileValidationResult;
            }
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
                    WritePasswordArgumentWarning();
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

        if (keyFilePath is not null)
        {
            int keyFileValidationResult = ValidateExistingKeyFilePath(keyFilePath);
            if (keyFileValidationResult != 0)
            {
                return keyFileValidationResult;
            }
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

    private async Task<int> RunInspectAsync(string[] args)
    {
        if (args.Length < 2)
        {
            return WriteError("Missing input path.");
        }

        string inputPath = args[1];

        if (args.Length > 2)
        {
            string extraArgument = args[2];
            return extraArgument.StartsWith("-", StringComparison.Ordinal)
                ? WriteError($"Unknown option '{extraArgument}'.")
                : WriteError("inspect accepts exactly one input path.");
        }

        if (!File.Exists(inputPath))
        {
            return WritePathError("The input file does not exist.");
        }

        FileCrypterFileInfo fileInfo = await FileCrypter.Core.FileCrypter
            .InspectAsync(inputPath)
            .ConfigureAwait(false);

        console.Out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Format version: {fileInfo.FormatVersion}"));
        console.Out.WriteLine($"Payload kind: {FormatPayloadKind(fileInfo.PayloadKind)}");
        console.Out.WriteLine($"Compression: {FormatCompressionAlgorithm(fileInfo.CompressionAlgorithm)}");
        console.Out.WriteLine($"Key file required: {FormatYesNo(fileInfo.IsKeyFileRequired)}");
        console.Out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Chunk size bytes: {fileInfo.ChunkSize}"));
        console.Out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Argon2id memory KiB: {fileInfo.Argon2MemoryKiB}"));
        console.Out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Argon2id iterations: {fileInfo.Argon2Iterations}"));
        console.Out.WriteLine(string.Create(CultureInfo.InvariantCulture, $"Argon2id parallelism: {fileInfo.Argon2Parallelism}"));
        console.Out.WriteLine(
            "Header status: unauthenticated. These values are only what the file claims. Run 'filecrypter verify <input>' to authenticate the payload.");
        return 0;
    }

    private async Task<int> RunVerifyAsync(string[] args)
    {
        if (args.Length < 2)
        {
            return WriteError("Missing input path.");
        }

        string inputPath = args[1];
        string? password = null;
        string? keyFilePath = null;

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
                    WritePasswordArgumentWarning();
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

                default:
                    return arg.StartsWith("-", StringComparison.Ordinal)
                        ? WriteError($"Unknown option '{arg}'.")
                        : WriteError("verify accepts exactly one input path.");
            }
        }

        password ??= ReadPasswordFromInteractiveConsole();

        if (string.IsNullOrEmpty(password))
        {
            return WriteError("A password is required. Use --password, --password-stdin, or run from an interactive terminal.");
        }

        if (!File.Exists(inputPath))
        {
            return WritePathError("The input file does not exist.");
        }

        if (keyFilePath is not null)
        {
            int keyFileValidationResult = ValidateExistingKeyFilePath(keyFilePath);
            if (keyFileValidationResult != 0)
            {
                return keyFileValidationResult;
            }
        }

        long inputLength = new FileInfo(inputPath).Length;
        (FileCrypterOptions verifyOptions, CliProgressReporter progressReporter) =
            CreateVerifyOptionsWithProgress(inputLength);

        FileCrypterVerifyResult result = keyFilePath is null
            ? await FileCrypter.Core.FileCrypter.VerifyFileAsync(
                inputPath,
                password,
                verifyOptions).ConfigureAwait(false)
            : await FileCrypter.Core.FileCrypter.VerifyFileAsync(
                inputPath,
                password,
                keyFilePath,
                verifyOptions).ConfigureAwait(false);

        progressReporter.ReportComplete();
        console.Out.WriteLine(
            string.Create(
                CultureInfo.InvariantCulture,
                $"Verified: {Path.GetFullPath(inputPath)} ({result.PlaintextBytesVerified} plaintext bytes authenticated)"));
        if (result.FileInfo.PayloadKind == FileCrypterPayloadKind.TarArchive)
        {
            console.Out.WriteLine(
                "Archive entries: not checked. Verification authenticates the archive bytes, not the archive entry structure.");
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
            WriteCompressionDefault(settings);
            WriteOverwriteDefault(settings);
            WriteDefaultOutputDirectory(settings);
            console.Out.WriteLine($"Settings file: {settingsStore.SettingsPath}");
            return 0;
        }

        if (args[1] == "set")
        {
            if (args.Length != 4)
            {
                return WriteSettingsUsageError();
            }

            return args[2] switch
            {
                "compression-default" => await SetCompressionDefaultAsync(args[3]).ConfigureAwait(false),
                "overwrite-default" => await SetOverwriteDefaultAsync(args[3]).ConfigureAwait(false),
                "output-directory" => await SetDefaultOutputDirectoryAsync(args[3]).ConfigureAwait(false),
                _ => WriteSettingsUsageError(),
            };
        }

        return WriteSettingsUsageError();
    }

    private async Task<int> SetCompressionDefaultAsync(string value)
    {
        if (!TryParseOnOff(value, out bool enableCompressionByDefault))
        {
            return WriteError("Compression default must be 'on' or 'off'.");
        }

        FileCrypterSettings existingSettings;
        try
        {
            existingSettings = await settingsStore.LoadAsync().ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return WriteSettingsWriteRefusedError();
        }

        var settings = new FileCrypterSettings
        {
            EnableCompressionByDefault = enableCompressionByDefault,
            NeverOverwriteExistingFilesByDefault = existingSettings.NeverOverwriteExistingFilesByDefault,
            DefaultOutputDirectory = existingSettings.DefaultOutputDirectory,
            ThemePreference = existingSettings.ThemePreference,
        };
        await settingsStore.SaveAsync(settings).ConfigureAwait(false);
        WriteCompressionDefault(settings);
        return 0;
    }

    private async Task<int> SetOverwriteDefaultAsync(string value)
    {
        if (!TryParseOnOff(value, out bool overwriteByDefault))
        {
            return WriteError("Overwrite default must be 'on' or 'off'.");
        }

        FileCrypterSettings existingSettings;
        try
        {
            existingSettings = await settingsStore.LoadAsync().ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return WriteSettingsWriteRefusedError();
        }

        var settings = new FileCrypterSettings
        {
            EnableCompressionByDefault = existingSettings.EnableCompressionByDefault,
            NeverOverwriteExistingFilesByDefault = !overwriteByDefault,
            DefaultOutputDirectory = existingSettings.DefaultOutputDirectory,
            ThemePreference = existingSettings.ThemePreference,
        };
        await settingsStore.SaveAsync(settings).ConfigureAwait(false);
        WriteOverwriteDefault(settings);
        return 0;
    }

    private async Task<int> SetDefaultOutputDirectoryAsync(string value)
    {
        string defaultOutputDirectory;
        if (value.Equals("clear", StringComparison.OrdinalIgnoreCase))
        {
            defaultOutputDirectory = string.Empty;
        }
        else
        {
            int validationResult = ValidateDefaultOutputDirectory(value, out defaultOutputDirectory);
            if (validationResult != 0)
            {
                return validationResult;
            }
        }

        FileCrypterSettings existingSettings;
        try
        {
            existingSettings = await settingsStore.LoadAsync().ConfigureAwait(false);
        }
        catch (InvalidDataException)
        {
            return WriteSettingsWriteRefusedError();
        }

        var settings = new FileCrypterSettings
        {
            EnableCompressionByDefault = existingSettings.EnableCompressionByDefault,
            NeverOverwriteExistingFilesByDefault = existingSettings.NeverOverwriteExistingFilesByDefault,
            DefaultOutputDirectory = defaultOutputDirectory,
            ThemePreference = existingSettings.ThemePreference,
        };
        await settingsStore.SaveAsync(settings).ConfigureAwait(false);
        WriteDefaultOutputDirectory(settings);
        return 0;
    }

    private int ValidateDefaultOutputDirectory(string value, out string defaultOutputDirectory)
    {
        defaultOutputDirectory = string.Empty;

        string fullOutputDirectory;
        try
        {
            fullOutputDirectory = Path.GetFullPath(value);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return WritePathError("The output directory path is invalid or inaccessible.");
        }

        if (File.Exists(fullOutputDirectory))
        {
            return WritePathError("The output directory path points to a file.");
        }

        if (!Directory.Exists(fullOutputDirectory))
        {
            return WritePathError("The output directory does not exist.");
        }

        if (new DirectoryInfo(fullOutputDirectory).LinkTarget is not null)
        {
            return WritePathError("The output directory must not be a symbolic link or reparse point.");
        }

        defaultOutputDirectory = fullOutputDirectory;
        return 0;
    }

    private void WriteCompressionDefault(FileCrypterSettings settings)
    {
        console.Out.WriteLine($"Compression default: {FormatOnOff(settings.EnableCompressionByDefault)}");
    }

    private void WriteOverwriteDefault(FileCrypterSettings settings)
    {
        console.Out.WriteLine($"Overwrite default: {FormatOnOff(!settings.NeverOverwriteExistingFilesByDefault)}");
    }

    private void WriteDefaultOutputDirectory(FileCrypterSettings settings)
    {
        console.Out.WriteLine(
            $"Default output directory: {(settings.DefaultOutputDirectory.Length == 0 ? "(not set)" : settings.DefaultOutputDirectory)}");
    }

    private int WriteSettingsUsageError()
    {
        console.Error.WriteLine("Unknown settings command. Use one of:");
        console.Error.WriteLine("  filecrypter settings show");
        console.Error.WriteLine("  filecrypter settings set compression-default <on|off>");
        console.Error.WriteLine("  filecrypter settings set overwrite-default <on|off>");
        console.Error.WriteLine("  filecrypter settings set output-directory <path|clear>");
        return 1;
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

    // Whole-segment match, so names such as "my..notes.txt" are allowed. Mirrors Core's internal FileCrypterPathGuard.
    private static void ThrowIfContainsParentSegment(string path)
    {
        if (path.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Contains(".."))
        {
            throw new ArgumentException("Invalid file path", nameof(path));
        }
    }

    private static bool PathsEqual(string left, string right)
    {
        StringComparison comparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;

        return string.Equals(left, right, comparison);
    }

    private int ValidateExistingKeyFilePath(string keyFilePath)
    {
        string fullKeyFilePath;
        try
        {
            fullKeyFilePath = Path.GetFullPath(keyFilePath);
        }
        catch (Exception exception) when (exception is ArgumentException or IOException or UnauthorizedAccessException)
        {
            return WritePathError("The key file path is invalid or inaccessible.");
        }

        string? keyFileDirectory = Path.GetDirectoryName(fullKeyFilePath);
        if (!string.IsNullOrEmpty(keyFileDirectory) && !Directory.Exists(keyFileDirectory))
        {
            return WritePathError("The key file directory does not exist.");
        }

        if (Directory.Exists(fullKeyFilePath))
        {
            return WritePathError("The key file path points to a directory.");
        }

        if (!File.Exists(fullKeyFilePath))
        {
            return WritePathError("The key file does not exist.");
        }

        return 0;
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

    private (FileCrypterOptions Options, CliProgressReporter Reporter) CreateVerifyOptionsWithProgress(long inputLength)
    {
        FileCrypterOptions sourceOptions = options ?? new FileCrypterOptions();
        var progressReporter = new CliProgressReporter(
            console.Error,
            "Verifying",
            inputLength,
            sourceOptions.Progress);

        var verifyOptions = new FileCrypterOptions
        {
            ChunkSize = sourceOptions.ChunkSize,
            Argon2MemoryKiB = sourceOptions.Argon2MemoryKiB,
            Argon2Iterations = sourceOptions.Argon2Iterations,
            Argon2Parallelism = sourceOptions.Argon2Parallelism,
            EnableCompression = sourceOptions.EnableCompression,
            Progress = progressReporter,
        };

        return (verifyOptions, progressReporter);
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

    private static string FormatYesNo(bool value)
    {
        return value ? "yes" : "no";
    }

    private static string FormatPayloadKind(FileCrypterPayloadKind payloadKind)
    {
        return payloadKind switch
        {
            FileCrypterPayloadKind.TarArchive => "tar archive",
            _ => "single file",
        };
    }

    private static string FormatCompressionAlgorithm(FileCrypterCompressionAlgorithm compressionAlgorithm)
    {
        return compressionAlgorithm switch
        {
            FileCrypterCompressionAlgorithm.Zstd => "zstd",
            _ => "none",
        };
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

    private int WriteFormatError(string command, FileCrypterFormatException exception)
    {
        console.Error.WriteLine($"{exception.Message} ({exception.Code})");
        string? hint = GetFormatTroubleshootingHint(command, exception.Code);
        console.Error.WriteLine(hint);
        return 1;
    }

    private static string GetFormatTroubleshootingHint(string command, FileCrypterFormatErrorCode errorCode)
    {
        return errorCode switch
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
            FileCrypterFormatErrorCode.UnsupportedPayloadKind =>
                command switch
                {
                    "decrypt" =>
                        "This encrypted file contains an archive. Use 'filecrypter archive-decrypt <input-archive> <output-directory>' to extract it.",
                    "batch-decrypt" =>
                        "This batch item contains an archive. Use 'filecrypter archive-decrypt <input-archive> <output-directory>' to extract it.",
                    "archive-decrypt" =>
                        "This encrypted file contains a single file. Use 'filecrypter decrypt <input> [output]' to decrypt it.",
                    "inspect" =>
                        "This FileCrypter build does not recognize that payload kind, so it cannot describe the file. The file was probably written by a newer FileCrypter version.",
                    "verify" =>
                        "This FileCrypter build cannot open that payload kind, so it cannot verify the file. Verify it with the FileCrypter version that wrote it.",
                    _ => "This FileCrypter build cannot open that payload yet.",
                },
            FileCrypterFormatErrorCode.UnsupportedVersion or
            FileCrypterFormatErrorCode.UnsupportedCompressionAlgorithm or
            FileCrypterFormatErrorCode.UnsupportedKeyFileRequirement =>
                "This FileCrypter build cannot open that payload yet.",
            _ => "The encrypted file metadata or payload is not valid for this FileCrypter version.",
        };
    }

    private int WritePathError(string message)
    {
        console.Error.WriteLine($"Path error: {message}");
        console.Error.WriteLine("Check that the input and output paths are files you can access, and that the output directory already exists.");
        return 1;
    }

    private int WritePathError(Exception exception)
    {
        return WritePathError(GetSafePathErrorMessage(exception));
    }

    private static string GetSafePathErrorMessage(Exception exception)
    {
        if (exception is FileNotFoundException)
        {
            return "The input file does not exist.";
        }

        if (exception is DirectoryNotFoundException)
        {
            return "The output directory does not exist.";
        }

        if (exception is UnauthorizedAccessException)
        {
            return "FileCrypter does not have permission to access one of the requested paths.";
        }

        string message = StripArgumentParameterSuffix(exception.Message);
        return IsSafePathErrorMessage(message)
            ? message
            : "FileCrypter could not access one of the requested paths.";
    }

    private static string StripArgumentParameterSuffix(string message)
    {
        int parameterIndex = message.IndexOf(" (Parameter '", StringComparison.Ordinal);
        return parameterIndex >= 0 ? message[..parameterIndex] : message;
    }

    private static bool IsSafePathErrorMessage(string message)
    {
        return message is
            "The input and output paths must be different." or
            "The key file and output paths must be different." or
            "The input path points to a directory." or
            "The input path must not be a symbolic link or reparse point." or
            "The input path must include a file name." or
            "The batch output directory path points to a file." or
            "The key file path points to a directory." or
            "The key file path must not be a symbolic link or reparse point." or
            "The key file could not be read completely." or
            "The output path must include a file name." or
            "The output path points to a directory." or
            "The output path must not be a symbolic link or reparse point." or
            "At least one input file is required." or
            "No available archive entry name could be found." or
            "No available auto-renamed output path could be found." ||
            message.StartsWith("A single batch run supports up to ", StringComparison.Ordinal) ||
            message.StartsWith("The key file is too large.", StringComparison.Ordinal);
    }

    private int WriteSettingsError()
    {
        console.Error.WriteLine("Settings error: FileCrypter could not read or write the local settings file.");
        console.Error.WriteLine("Repair or delete the settings file, then run 'filecrypter settings set compression-default on' or 'off' to recreate it.");
        return 1;
    }

    private int WriteSettingsWriteRefusedError()
    {
        console.Error.WriteLine("Settings error: FileCrypter could not read or write the local settings file.");
        console.Error.WriteLine("No setting was changed. Saving now would discard the stored settings FileCrypter cannot read.");
        console.Error.WriteLine("Repair or delete the settings file, then run 'filecrypter settings set compression-default on' or 'off' to recreate it.");
        return 1;
    }

    private int WriteOperationError()
    {
        console.Error.WriteLine("Operation error: FileCrypter could not complete the requested operation.");
        console.Error.WriteLine("Try again with accessible input and output paths, or use --help to verify the command shape.");
        return 1;
    }

    private void WriteBatchItemError(string command, FileCrypterBatchItemResult item)
    {
        Exception exception = item.Error ?? new InvalidOperationException("The batch item failed without an error.");
        FileCrypterFormatException? formatError = exception as FileCrypterFormatException;
        string message = formatError is not null
            ? $"{formatError.Message} ({formatError.Code})"
            : GetSafePathErrorMessage(exception);
        console.Error.WriteLine($"Failed: {item.InputPath}");
        console.Error.WriteLine(message);
        if (formatError is not null)
        {
            console.Error.WriteLine(GetFormatTroubleshootingHint(command, formatError.Code));
        }
        else
        {
            console.Error.WriteLine("Check that this input path is a file you can access and that the output directory already exists.");
        }
    }

    private void WritePasswordArgumentWarning()
    {
        console.Error.WriteLine(
            "Warning: --password can expose secrets in shell history or process listings. Prefer the hidden prompt or --password-stdin.");
    }

    private void WriteUsage()
    {
        console.Out.WriteLine(
            """
            Usage:
              filecrypter encrypt <input> [output] [--password-stdin | --password <password>] [--key-file <path> | --generate-key-file <path>] [--compress] [--overwrite]
              filecrypter decrypt <input> [output] [--password-stdin | --password <password>] [--key-file <path>] [--overwrite]
              filecrypter inspect <input>
              filecrypter verify <input> [--password-stdin | --password <password>] [--key-file <path>]
              filecrypter batch-encrypt <output-directory> <input>... [--password-stdin | --password <password>] [--key-file <path>] [--overwrite]
              filecrypter batch-decrypt <output-directory> <input>... [--password-stdin | --password <password>] [--key-file <path>] [--overwrite]
              filecrypter archive-encrypt <output-archive-or-directory> <input>... [--archive-name <name>] [--password-stdin | --password <password>] [--key-file <path>] [--overwrite]
              filecrypter archive-decrypt <input-archive> <output-directory> [--password-stdin | --password <password>] [--key-file <path>] [--overwrite]
              filecrypter settings show
              filecrypter settings set compression-default <on|off>
              filecrypter settings set overwrite-default <on|off>
              filecrypter settings set output-directory <path|clear>

            If no password option is supplied, FileCrypter prompts without echoing the password when run interactively.
            For automation, pipe one password line to --password-stdin. Avoid --password when possible because command-line
            arguments can be exposed through shell history, process listings, logs, terminal scrollback, and crash reports.
            If output is omitted, encryption appends .encrypted and decryption removes .encrypted when present.
            Use --compress during encryption to reduce compatible payloads before encryption. Decryption detects compressed files automatically.
            Use inspect to print the unencrypted header metadata of an encrypted file. It needs no password and reads only
            the 64-byte header, so it reports what the file claims and never proves the file is intact or genuine.
            Use verify to decrypt an encrypted file to nothing and confirm every chunk authenticates. It writes no files and
            needs the same password and key file that decryption needs. For archives it authenticates the archive bytes but
            does not check the archive entry structure.
            Set compression-default on to compress single-file encryption by default.
            Set overwrite-default on to replace existing outputs by default; off keeps existing files and auto-renames instead.
            Set output-directory to an existing directory to preselect it, or to 'clear' to remove it.
            Settings are shared with the FileCrypter desktop app; overwrite-default and output-directory are stored for it and
            do not change what an explicit CLI option does.
            Batch encryption compresses each file automatically and writes one output path per successful file to stdout.
            Archive encryption writes one compressed tar archive payload and archive decryption writes each extracted path to stdout.
            If archive-encrypt receives an output directory, it creates a timestamped .tar.zst.encrypted archive there.
            Use --archive-name with an output directory to choose a safe custom archive basename.
            Use archive-decrypt for .tar.zst.encrypted archives; use decrypt for single-file .encrypted files.
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

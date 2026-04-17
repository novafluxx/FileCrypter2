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
            return WriteError($"{exception.Message} ({exception.Code})");
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return WriteError(exception.Message);
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
        bool overwrite = false;

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

                case "--overwrite":
                    overwrite = true;
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
        string finalOutputPath;

        if (encrypt)
        {
            finalOutputPath = await FileCrypter.Core.FileCrypter.EncryptFileAsync(
                inputPath,
                outputPath,
                password,
                options,
                overwrite).ConfigureAwait(false);
        }
        else
        {
            finalOutputPath = await FileCrypter.Core.FileCrypter.DecryptFileAsync(
                inputPath,
                outputPath,
                password,
                options,
                overwrite).ConfigureAwait(false);
        }

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

    private int WriteError(string message)
    {
        console.Error.WriteLine(message);
        return 1;
    }

    private void WriteUsage()
    {
        console.Out.WriteLine(
            """
            Usage:
              filecrypter encrypt <input> [output] [--password <password> | --password-stdin] [--overwrite]
              filecrypter decrypt <input> [output] [--password <password> | --password-stdin] [--overwrite]

            If no password option is supplied, FileCrypter prompts without echoing the password when run interactively.
            If output is omitted, encryption appends .encrypted and decryption removes .encrypted when present.
            """);
    }
}

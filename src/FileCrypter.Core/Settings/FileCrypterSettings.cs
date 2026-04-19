namespace FileCrypter.Core.Settings;

public sealed class FileCrypterSettings
{
    public bool EnableCompressionByDefault { get; init; }

    public bool NeverOverwriteExistingFilesByDefault { get; init; } = true;

    public string DefaultOutputDirectory { get; init; } = string.Empty;
}

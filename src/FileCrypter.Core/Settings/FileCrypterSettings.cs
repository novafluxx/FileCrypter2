namespace FileCrypter.Core.Settings;

public sealed class FileCrypterSettings
{
    public bool EnableCompressionByDefault { get; init; }

    public bool NeverOverwriteExistingFilesByDefault { get; init; } = true;

    public string DefaultOutputDirectory { get; init; } = string.Empty;

    public FileCrypterThemePreference ThemePreference { get; init; } = FileCrypterThemePreference.System;

    public FileCrypterSettings Normalize()
    {
        return new FileCrypterSettings
        {
            EnableCompressionByDefault = EnableCompressionByDefault,
            NeverOverwriteExistingFilesByDefault = NeverOverwriteExistingFilesByDefault,
            DefaultOutputDirectory = string.IsNullOrWhiteSpace(DefaultOutputDirectory)
                ? string.Empty
                : DefaultOutputDirectory.Trim(),
            ThemePreference = Enum.IsDefined(ThemePreference)
                ? ThemePreference
                : FileCrypterThemePreference.System,
        };
    }
}

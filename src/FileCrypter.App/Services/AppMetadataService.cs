namespace FileCrypter.App.Services;

public sealed class AppMetadataService : IAppMetadataService
{
    private const ushort CurrentFormatVersion = 1;
    private readonly Lazy<string> displayVersion = new(CreateDisplayVersion);

    public string DisplayVersion => displayVersion.Value;

    public ushort FormatVersion => CurrentFormatVersion;

    private static string CreateDisplayVersion()
    {
        Version? version = typeof(AppMetadataService).Assembly.GetName().Version;
        if (version is null || version.Major < 0 || version.Minor < 0)
        {
            return "v0.1.0";
        }

        return version.Build >= 0
            ? $"v{version.Major}.{version.Minor}.{version.Build}"
            : $"v{version.Major}.{version.Minor}";
    }
}

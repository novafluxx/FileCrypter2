namespace FileCrypter.Desktop.Services;

public interface IAppMetadataService
{
    string DisplayVersion { get; }

    ushort FormatVersion { get; }
}

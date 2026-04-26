namespace FileCrypter.App.Services;

public interface IAppMetadataService
{
    string DisplayVersion { get; }

    ushort FormatVersion { get; }
}

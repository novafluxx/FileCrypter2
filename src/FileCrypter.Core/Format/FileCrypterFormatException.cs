namespace FileCrypter.Core.Format;

internal sealed class FileCrypterFormatException : Exception
{
    public FileCrypterFormatException(FileCrypterFormatErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public FileCrypterFormatErrorCode Code { get; }
}

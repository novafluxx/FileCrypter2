namespace FileCrypter.Core.Format;

public sealed class FileCrypterFormatException : Exception
{
    public FileCrypterFormatException(FileCrypterFormatErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    public FileCrypterFormatException(FileCrypterFormatErrorCode code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    public FileCrypterFormatErrorCode Code { get; }
}

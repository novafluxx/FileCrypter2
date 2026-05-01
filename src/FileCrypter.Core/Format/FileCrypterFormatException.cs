namespace FileCrypter.Core.Format;

/// <summary>
/// Represents a FileCrypter payload, header, authentication, compression, or archive format failure.
/// </summary>
/// <remarks>
/// Decryption APIs throw this exception when an encrypted input is unsupported, truncated, unauthenticated, compressed
/// incorrectly, or contains an unsafe archive payload. Use <see cref="Code"/> for stable handling instead of parsing
/// localized or host-facing messages.
/// </remarks>
public sealed class FileCrypterFormatException : Exception
{
    /// <summary>
    /// Initializes a new instance of the <see cref="FileCrypterFormatException"/> class with an error code and message.
    /// </summary>
    /// <param name="code">The stable format error code.</param>
    /// <param name="message">The diagnostic message for the failure.</param>
    public FileCrypterFormatException(FileCrypterFormatErrorCode code, string message)
        : base(message)
    {
        Code = code;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="FileCrypterFormatException"/> class with an error code, message, and inner exception.
    /// </summary>
    /// <param name="code">The stable format error code.</param>
    /// <param name="message">The diagnostic message for the failure.</param>
    /// <param name="innerException">The exception that caused this format failure.</param>
    public FileCrypterFormatException(FileCrypterFormatErrorCode code, string message, Exception innerException)
        : base(message, innerException)
    {
        Code = code;
    }

    /// <summary>
    /// Gets the stable FileCrypter format error code.
    /// </summary>
    public FileCrypterFormatErrorCode Code { get; }
}

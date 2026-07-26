namespace FileCrypter.Core;

/// <summary>
/// Identifies the kind of plaintext payload stored inside a FileCrypter encrypted file.
/// </summary>
/// <remarks>
/// The payload kind is recorded in the authenticated 64-byte FileCrypter header and can be read without a password. It
/// selects which decryption entry point a host must use: single-file payloads are decrypted with the FileCrypter file
/// decryption methods, while archive payloads must be decrypted with the archive decryption methods.
/// </remarks>
public enum FileCrypterPayloadKind
{
    /// <summary>
    /// The payload is the contents of a single plaintext file.
    /// </summary>
    SingleFile = 1,

    /// <summary>
    /// The payload is a tar archive containing one or more plaintext files.
    /// </summary>
    TarArchive = 2,
}

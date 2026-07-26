namespace FileCrypter.Core;

/// <summary>
/// Identifies the compression algorithm applied to a FileCrypter payload before encryption.
/// </summary>
/// <remarks>
/// The compression algorithm is recorded in the authenticated 64-byte FileCrypter header and can be read without a
/// password. Decryption always replays the recorded algorithm; <see cref="FileCrypterOptions.EnableCompression"/> only
/// affects encryption.
/// </remarks>
public enum FileCrypterCompressionAlgorithm
{
    /// <summary>
    /// The payload was encrypted without compression.
    /// </summary>
    None = 0,

    /// <summary>
    /// The payload was compressed with Zstandard before encryption.
    /// </summary>
    Zstd = 1,
}

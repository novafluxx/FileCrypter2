namespace FileCrypter.Core;

/// <summary>
/// Describes the outcome of a successful FileCrypter verification pass.
/// </summary>
/// <remarks>
/// A result is only produced when every encrypted chunk authenticated, the final-chunk framing was valid, and no
/// trailing data followed the payload. Verification failures throw
/// <see cref="Format.FileCrypterFormatException"/> instead of returning a result, so this type never represents a
/// failed verification.
/// </remarks>
public sealed record FileCrypterVerifyResult
{
    internal FileCrypterVerifyResult(FileCrypterFileInfo fileInfo, long plaintextBytesVerified)
    {
        FileInfo = fileInfo;
        PlaintextBytesVerified = plaintextBytesVerified;
    }

    /// <summary>
    /// Gets the header metadata projected from the verified encrypted file.
    /// </summary>
    /// <remarks>
    /// This is the same projection <see cref="FileCrypter.InspectAsync"/> returns for the file, read before key
    /// derivation began.
    /// </remarks>
    public FileCrypterFileInfo FileInfo { get; }

    /// <summary>
    /// Gets the number of authenticated plaintext bytes produced and discarded during verification.
    /// </summary>
    /// <remarks>
    /// The count is measured after decompression, so for a compressed payload it is the decompressed plaintext length,
    /// not the encrypted file length. For a
    /// <see cref="FileCrypterPayloadKind.TarArchive"/> payload it is the length of the tar stream inside the payload,
    /// including tar headers and padding, not the total size of the archived files.
    /// </remarks>
    public long PlaintextBytesVerified { get; }
}

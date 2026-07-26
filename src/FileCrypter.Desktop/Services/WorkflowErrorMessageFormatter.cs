using FileCrypter.Core.Format;

namespace FileCrypter.Desktop.Services;

internal static class WorkflowErrorMessageFormatter
{
    public const string ClipboardCopyFailureMessage = "Clipboard copy failed.";

    public const string PathRevealFailureMessage = "Could not reveal that path.";

    public const string PickerFailureMessage = "Could not open the file picker.";

    public static string GetTroubleshootingMessage(Exception exception)
    {
        if (exception is FileCrypterFormatException formatException)
        {
            return formatException.Code switch
            {
                FileCrypterFormatErrorCode.AuthenticationFailed =>
                    "Check the password and key file, then try again.",
                FileCrypterFormatErrorCode.KeyFileRequired =>
                    "Provide the matching key file.",
                FileCrypterFormatErrorCode.InvalidMagic =>
                    "Choose a FileCrypter .encrypted file produced by this app.",
                FileCrypterFormatErrorCode.TruncatedHeader or FileCrypterFormatErrorCode.TruncatedChunk =>
                    "The encrypted file appears incomplete or damaged. Try a fresh copy of the file.",
                FileCrypterFormatErrorCode.UnsupportedPayloadKind =>
                    "This file belongs to a different FileCrypter workflow. Use archive extraction for encrypted archives and standard decrypt for single-file outputs.",
                FileCrypterFormatErrorCode.UnsupportedVersion =>
                    "This file was written by a newer FileCrypter file format. Update FileCrypter, then try again.",
                FileCrypterFormatErrorCode.UnsupportedAeadAlgorithm
                    or FileCrypterFormatErrorCode.UnsupportedKdfAlgorithm
                    or FileCrypterFormatErrorCode.UnsupportedCompressionAlgorithm
                    or FileCrypterFormatErrorCode.UnsupportedArgon2Version =>
                    "This file uses an algorithm this FileCrypter version cannot read. Update FileCrypter, then try again.",
                FileCrypterFormatErrorCode.InvalidHeaderLength
                    or FileCrypterFormatErrorCode.InvalidFlags
                    or FileCrypterFormatErrorCode.InvalidReservedBytes
                    or FileCrypterFormatErrorCode.InvalidKeyFileHashAlgorithm =>
                    "The file header is not a valid FileCrypter header. The file was probably damaged or altered. Try a fresh copy of the file.",
                FileCrypterFormatErrorCode.InvalidChunkSize
                    or FileCrypterFormatErrorCode.InvalidArgon2Parameters =>
                    "The file header asks for settings outside the range FileCrypter supports. The file was probably damaged or altered. Try a fresh copy of the file.",
                FileCrypterFormatErrorCode.UnsupportedKeyFileRequirement =>
                    "This file's key-file setting is not supported for this operation. Use the FileCrypter workflow that matches how the file was created.",
                FileCrypterFormatErrorCode.InvalidChunkFlags
                    or FileCrypterFormatErrorCode.InvalidChunkLength
                    or FileCrypterFormatErrorCode.InvalidChunkReservedBytes
                    or FileCrypterFormatErrorCode.TooManyChunks
                    or FileCrypterFormatErrorCode.TrailingData =>
                    "The encrypted payload is framed incorrectly and may have been damaged in transit. Try a fresh copy of the file.",
                FileCrypterFormatErrorCode.InvalidCompressedPayload =>
                    "The file decrypted, but its compressed contents could not be expanded. Try a fresh copy of the file.",
                FileCrypterFormatErrorCode.InvalidArchivePayload =>
                    "The archive contents are not a supported, safe FileCrypter archive. Try a fresh copy of the archive.",
                _ => "The encrypted file metadata or payload is not valid for this FileCrypter version.",
            };
        }

        if (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return "FileCrypter could not access one of the selected paths. Check that the file or folder exists and that you have permission to use it.";
        }

        return "The operation failed. Check the selected files and settings, then try again.";
    }

    public static string GetSettingsFailureMessage(Exception exception)
    {
        return exception is DirectoryNotFoundException
            ? "Settings error: The default output directory does not exist. Choose an existing folder."
            : "Settings error: FileCrypter could not save or load settings. Check that the settings file is available and writable.";
    }

    public static string GetUpdateFailureDetail(Exception exception)
    {
        _ = exception;
        return "The update status could not be refreshed. Check the configured update channel and try again.";
    }
}

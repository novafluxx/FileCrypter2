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

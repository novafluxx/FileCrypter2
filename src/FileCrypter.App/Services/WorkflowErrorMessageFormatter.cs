using FileCrypter.Core.Format;

namespace FileCrypter.App.Services;

internal static class WorkflowErrorMessageFormatter
{
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
                    "This file needs an archive workflow that is not available on this page yet.",
                _ => "The encrypted file metadata or payload is not valid for this FileCrypter version.",
            };
        }

        if (exception is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return $"Path error: {exception.Message}";
        }

        return exception.Message;
    }
}

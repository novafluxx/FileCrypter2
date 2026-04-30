using System.Globalization;

namespace FileCrypter.Desktop.Services;

internal static class ArchiveFileNameHelper
{
    private const string DefaultArchiveEncryptedSuffix = ".tar.zst.encrypted";
    private const string GeneratedArchiveNamePrefix = "filecrypter-archive-";
    private const string ArchiveTimestampFormat = "yyyyMMdd-HHmmss";
    private const string CrossPlatformInvalidArchiveNameCharacters = "<>:\"/\\|?*";

    public static bool TryCreateArchiveFileName(
        string? archiveName,
        out string archiveFileName,
        out string? error)
    {
        if (string.IsNullOrWhiteSpace(archiveName))
        {
            archiveFileName = GeneratedArchiveNamePrefix +
                DateTimeOffset.Now.ToString(ArchiveTimestampFormat, CultureInfo.InvariantCulture) +
                DefaultArchiveEncryptedSuffix;
            error = null;
            return true;
        }

        string trimmedArchiveName = archiveName.Trim();
        string archiveBaseName = trimmedArchiveName.EndsWith(DefaultArchiveEncryptedSuffix, StringComparison.OrdinalIgnoreCase)
            ? trimmedArchiveName[..^DefaultArchiveEncryptedSuffix.Length]
            : trimmedArchiveName;
        if (string.IsNullOrWhiteSpace(archiveBaseName))
        {
            archiveFileName = string.Empty;
            error = "Archive name must include a file name before .tar.zst.encrypted.";
            return false;
        }

        if (!IsSafeArchiveFileName(trimmedArchiveName))
        {
            archiveFileName = string.Empty;
            error = "Archive name contains characters or reserved words that are unsafe in file names.";
            return false;
        }

        archiveFileName = archiveBaseName + DefaultArchiveEncryptedSuffix;
        error = null;
        return true;
    }

    private static bool IsSafeArchiveFileName(string fileName)
    {
        if (fileName is "." or ".." ||
            fileName.EndsWith(' ') ||
            fileName.EndsWith('.'))
        {
            return false;
        }

        if (fileName.Any(character =>
            char.IsControl(character) ||
            CrossPlatformInvalidArchiveNameCharacters.Contains(character)))
        {
            return false;
        }

        string firstNamePart = fileName.Split('.')[0].TrimEnd(' ');
        return !IsReservedWindowsFileName(firstNamePart);
    }

    private static bool IsReservedWindowsFileName(string fileName)
    {
        return fileName.Equals("CON", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("PRN", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("AUX", StringComparison.OrdinalIgnoreCase) ||
            fileName.Equals("NUL", StringComparison.OrdinalIgnoreCase) ||
            IsReservedWindowsPortName(fileName, "COM") ||
            IsReservedWindowsPortName(fileName, "LPT");
    }

    private static bool IsReservedWindowsPortName(string fileName, string prefix)
    {
        return fileName.Length == prefix.Length + 1 &&
            fileName.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) &&
            fileName[^1] is >= '1' and <= '9';
    }
}

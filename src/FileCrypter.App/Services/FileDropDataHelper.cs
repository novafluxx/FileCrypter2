using Avalonia.Input;
using Avalonia.Platform.Storage;

namespace FileCrypter.App.Services;

internal static class FileDropDataHelper
{
    private static readonly StringComparer PathComparer = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS()
        ? StringComparer.OrdinalIgnoreCase
        : StringComparer.Ordinal;

    public static IReadOnlyList<string> GetLocalFilePaths(IDataTransfer? data)
    {
        if (data is null)
        {
            return [];
        }

        IReadOnlyList<IStorageItem>? storageItems = data.TryGetFiles();
        if (storageItems is null || storageItems.Count == 0)
        {
            return [];
        }

        List<string> paths = [];
        foreach (IStorageItem item in storageItems)
        {
            string? localPath = item.TryGetLocalPath();
            if (string.IsNullOrWhiteSpace(localPath))
            {
                continue;
            }

            string fullPath = Path.GetFullPath(localPath);
            if (!File.Exists(fullPath) || paths.Any(existingPath => PathComparer.Equals(existingPath, fullPath)))
            {
                continue;
            }

            paths.Add(fullPath);
        }

        return paths;
    }
}

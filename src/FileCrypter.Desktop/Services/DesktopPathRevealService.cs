using System.Diagnostics;

namespace FileCrypter.Desktop.Services;

public sealed class DesktopPathRevealService : IPathRevealService
{
    public Task<bool> TryRevealPathAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.FromResult(false);
        }

        try
        {
            string fullPath = Path.GetFullPath(path);

            bool revealed = OperatingSystem.IsWindows()
                ? RevealOnWindows(fullPath)
                : OperatingSystem.IsMacOS()
                    ? RevealOnMac(fullPath)
                    : RevealOnLinux(fullPath);

            return Task.FromResult(revealed);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }

    private static bool RevealOnWindows(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            StartProcess("explorer.exe", $"/select,\"{fullPath}\"");
            return true;
        }

        string targetDirectory = ResolveDirectoryTarget(fullPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            StartProcess("explorer.exe", $"\"{targetDirectory}\"");
            return true;
        }

        return false;
    }

    private static bool RevealOnMac(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            StartProcess("open", $"-R \"{fullPath}\"");
            return true;
        }

        string targetDirectory = ResolveDirectoryTarget(fullPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            StartProcess("open", $"\"{targetDirectory}\"");
            return true;
        }

        return false;
    }

    private static bool RevealOnLinux(string fullPath)
    {
        string targetDirectory = ResolveDirectoryTarget(fullPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            StartProcess("xdg-open", $"\"{targetDirectory}\"");
            return true;
        }

        return false;
    }

    private static string ResolveDirectoryTarget(string fullPath)
    {
        if (Directory.Exists(fullPath))
        {
            return fullPath;
        }

        string? parentDirectory = Path.GetDirectoryName(fullPath);
        return !string.IsNullOrWhiteSpace(parentDirectory) && Directory.Exists(parentDirectory)
            ? parentDirectory
            : string.Empty;
    }

    private static void StartProcess(string fileName, string arguments)
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = fileName,
            Arguments = arguments,
            UseShellExecute = false,
        });
    }
}

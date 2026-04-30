using System.Diagnostics;

namespace FileCrypter.Desktop.Services;

public sealed class DesktopPathRevealService : IPathRevealService
{
    public Task TryRevealPathAsync(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(path))
        {
            return Task.CompletedTask;
        }

        try
        {
            string fullPath = Path.GetFullPath(path);

            if (OperatingSystem.IsWindows())
            {
                RevealOnWindows(fullPath);
            }
            else if (OperatingSystem.IsMacOS())
            {
                RevealOnMac(fullPath);
            }
            else
            {
                RevealOnLinux(fullPath);
            }
        }
        catch
        {
            // Reveal is best-effort. Keep failures quiet so the workflow status stays focused.
        }

        return Task.CompletedTask;
    }

    private static void RevealOnWindows(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            StartProcess("explorer.exe", $"/select,\"{fullPath}\"");
            return;
        }

        string targetDirectory = ResolveDirectoryTarget(fullPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            StartProcess("explorer.exe", $"\"{targetDirectory}\"");
        }
    }

    private static void RevealOnMac(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            StartProcess("open", $"-R \"{fullPath}\"");
            return;
        }

        string targetDirectory = ResolveDirectoryTarget(fullPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            StartProcess("open", $"\"{targetDirectory}\"");
        }
    }

    private static void RevealOnLinux(string fullPath)
    {
        string targetDirectory = ResolveDirectoryTarget(fullPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            StartProcess("xdg-open", $"\"{targetDirectory}\"");
        }
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

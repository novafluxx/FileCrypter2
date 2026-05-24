using System.Diagnostics;

namespace FileCrypter.Desktop.Services;

public sealed class DesktopPathRevealService : IPathRevealService
{
    private enum PathRevealCommand
    {
        WindowsExplorer,
        MacOpen,
        LinuxXdgOpen,
    }

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
            StartProcess(PathRevealCommand.WindowsExplorer, "/select,", fullPath);
            return true;
        }

        string targetDirectory = ResolveDirectoryTarget(fullPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            StartProcess(PathRevealCommand.WindowsExplorer, targetDirectory);
            return true;
        }

        return false;
    }

    private static bool RevealOnMac(string fullPath)
    {
        if (File.Exists(fullPath))
        {
            StartProcess(PathRevealCommand.MacOpen, "-R", fullPath);
            return true;
        }

        string targetDirectory = ResolveDirectoryTarget(fullPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            StartProcess(PathRevealCommand.MacOpen, targetDirectory);
            return true;
        }

        return false;
    }

    private static bool RevealOnLinux(string fullPath)
    {
        string targetDirectory = ResolveDirectoryTarget(fullPath);
        if (!string.IsNullOrWhiteSpace(targetDirectory))
        {
            StartProcess(PathRevealCommand.LinuxXdgOpen, targetDirectory);
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

    private static void StartProcess(PathRevealCommand command, params string[] arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = GetAllowedExecutable(command),
            UseShellExecute = false,
        };

        foreach (string argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        // nosec AIK_csharp_CommandInjection: executable is selected from PathRevealCommand allow-list; paths are normalized, existence-checked, and passed via ArgumentList.
        Process.Start(startInfo);
    }

    private static string GetAllowedExecutable(PathRevealCommand command)
    {
        return command switch
        {
            PathRevealCommand.WindowsExplorer => "explorer.exe",
            PathRevealCommand.MacOpen => "open",
            PathRevealCommand.LinuxXdgOpen => "xdg-open",
            _ => throw new ArgumentOutOfRangeException(nameof(command), command, "Unsupported path reveal command."),
        };
    }
}

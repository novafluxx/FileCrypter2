using System.Diagnostics;

namespace FileCrypter.App.Tests.Packaging;

public sealed class MacAppBundleTests
{
    [Fact]
    public void PackageScript_CreatesExpectedMacAppBundle()
    {
        if (!OperatingSystem.IsMacOS())
        {
            return;
        }

        string repoRoot = FindRepositoryRoot();
        string scriptPath = Path.Combine(repoRoot, "scripts", "package-macos-app.sh");
        string bundlePath = Path.Combine(repoRoot, "artifacts", "macos", "FileCrypter.app");
        string infoPlistPath = Path.Combine(bundlePath, "Contents", "Info.plist");
        string macOsPath = Path.Combine(bundlePath, "Contents", "MacOS");
        string executablePath = Path.Combine(macOsPath, "FileCrypter.App");

        Assert.True(File.Exists(scriptPath), $"Expected packaging script at {scriptPath}");

        ProcessStartInfo startInfo = new("/bin/bash")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        startInfo.ArgumentList.Add(scriptPath);

        using Process process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("Failed to start packaging script.");

        string standardOutput = process.StandardOutput.ReadToEnd();
        string standardError = process.StandardError.ReadToEnd();
        bool exited = process.WaitForExit(milliseconds: 600_000);

        Assert.True(exited, "Packaging script did not exit within 10 minutes.");
        Assert.True(
            process.ExitCode == 0,
            $"Packaging script failed with exit code {process.ExitCode}.{Environment.NewLine}stdout:{Environment.NewLine}{standardOutput}{Environment.NewLine}stderr:{Environment.NewLine}{standardError}");
        Assert.Contains("Created macOS app bundle:", standardOutput, StringComparison.Ordinal);
        Assert.True(Directory.Exists(bundlePath), $"Expected bundle at {bundlePath}");
        Assert.True(File.Exists(infoPlistPath), $"Expected Info.plist at {infoPlistPath}");
        Assert.True(Directory.Exists(Path.Combine(bundlePath, "Contents", "Resources")));
        Assert.True(Directory.Exists(macOsPath), $"Expected MacOS directory at {macOsPath}");
        Assert.True(File.Exists(executablePath), $"Expected executable at {executablePath}");

        UnixFileMode executableMode = File.GetUnixFileMode(executablePath);
        UnixFileMode executeBits = UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute;
        Assert.True((executableMode & executeBits) != 0, "Expected the apphost to be executable.");
        Assert.True(File.Exists(Path.Combine(macOsPath, "FileCrypter.App.dll")));
        Assert.True(File.Exists(Path.Combine(macOsPath, "FileCrypter.Core.dll")));
        Assert.True(File.Exists(Path.Combine(macOsPath, "Avalonia.dll")));
        Assert.False(File.Exists(Path.Combine(macOsPath, "FileCrypter.Cli")));
        Assert.False(File.Exists(Path.Combine(macOsPath, "FileCrypter.Cli.dll")));

        string infoPlist = File.ReadAllText(infoPlistPath);

        Assert.Contains("<string>FileCrypter</string>", infoPlist, StringComparison.Ordinal);
        Assert.Contains("<string>FileCrypter.App</string>", infoPlist, StringComparison.Ordinal);
        Assert.Contains("<string>com.novafluxx.filecrypter</string>", infoPlist, StringComparison.Ordinal);
        Assert.Contains("<string>0.1.0</string>", infoPlist, StringComparison.Ordinal);
        Assert.Contains("<string>13.0</string>", infoPlist, StringComparison.Ordinal);

        ProcessStartInfo verifyStartInfo = new("/usr/bin/codesign")
        {
            WorkingDirectory = repoRoot,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
        verifyStartInfo.ArgumentList.Add("--verify");
        verifyStartInfo.ArgumentList.Add("--deep");
        verifyStartInfo.ArgumentList.Add("--strict");
        verifyStartInfo.ArgumentList.Add("--verbose=4");
        verifyStartInfo.ArgumentList.Add(bundlePath);

        using Process verifyProcess = Process.Start(verifyStartInfo)
            ?? throw new InvalidOperationException("Failed to start codesign verification.");

        string verifyOutput = verifyProcess.StandardOutput.ReadToEnd();
        string verifyError = verifyProcess.StandardError.ReadToEnd();
        bool verifyExited = verifyProcess.WaitForExit(milliseconds: 60_000);

        Assert.True(verifyExited, "codesign verification did not exit within 60 seconds.");
        Assert.True(
            verifyProcess.ExitCode == 0,
            $"codesign verification failed with exit code {verifyProcess.ExitCode}.{Environment.NewLine}stdout:{Environment.NewLine}{verifyOutput}{Environment.NewLine}stderr:{Environment.NewLine}{verifyError}");
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "FileCrypterDotNet.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find the repository root from the test output directory.");
    }
}

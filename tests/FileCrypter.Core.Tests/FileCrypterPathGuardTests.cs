namespace FileCrypter.Core.Tests;

public sealed class FileCrypterPathGuardTests
{
    [Theory]
    [InlineData("..")]
    [InlineData("../escape.txt")]
    [InlineData("dir/../escape.txt")]
    [InlineData("dir/..")]
    public void ThrowIfContainsParentSegment_WithParentSegment_Throws(string path)
    {
        Assert.Throws<ArgumentException>(() => FileCrypterPathGuard.ThrowIfContainsParentSegment(path));
    }

    [Fact]
    public void ThrowIfContainsParentSegment_WithPlatformSeparatorParentSegment_Throws()
    {
        string path = string.Join(Path.DirectorySeparatorChar, "root", "dir", "..", "file.txt");

        Assert.Throws<ArgumentException>(() => FileCrypterPathGuard.ThrowIfContainsParentSegment(path));
    }

    [Theory]
    [InlineData("my..notes.txt")]
    [InlineData("dir..name/file.txt")]
    [InlineData("dir/..hidden")]
    [InlineData("dir/trailing..")]
    [InlineData("...")]
    [InlineData("dir/./file.txt")]
    public void ThrowIfContainsParentSegment_WithDoubleDotsInsideNames_DoesNotThrow(string path)
    {
        FileCrypterPathGuard.ThrowIfContainsParentSegment(path);
    }

    [Fact]
    public void ThrowIfContainsParentSegment_WithFullPathFromGetFullPath_DoesNotThrow()
    {
        string path = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "dir", "..", "my..notes.txt"));

        FileCrypterPathGuard.ThrowIfContainsParentSegment(path);
    }
}

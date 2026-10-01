namespace FileCrypter.Core;

internal static class FileCrypterPathGuard
{
    private static readonly char[] SeparatorChars = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

    /// <summary>
    /// Rejects a path that contains a parent-directory (<c>..</c>) segment.
    /// </summary>
    /// <remarks>
    /// Matches whole segments only, so names such as <c>my..notes.txt</c> are allowed. Callers pass full paths from
    /// <see cref="Path.GetFullPath(string)"/>, which have no parent segments left; this is a defensive backstop, not
    /// the traversal defense for archive entries (see <c>CreateArchiveExtractionOutputPath</c>).
    /// </remarks>
    internal static void ThrowIfContainsParentSegment(string path)
    {
        ArgumentNullException.ThrowIfNull(path);

        foreach (string segment in path.Split(SeparatorChars))
        {
            if (segment == "..")
            {
                throw new ArgumentException("Invalid file path", nameof(path));
            }
        }
    }
}

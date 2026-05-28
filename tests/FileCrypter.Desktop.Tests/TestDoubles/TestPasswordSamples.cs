namespace FileCrypter.Desktop.Tests.TestDoubles;

internal static class TestPasswordSamples
{
    public static string Changed => string.Concat("different", "-", "sample");

    public static string Strong => string.Concat("Strong", "!", "Pass", "123");

    public static string GeneratedRandom => string.Concat("Generated", "!", "Sample", "123456");

    public static string MemorablePassphrase => string.Join(
        "-",
        "river",
        "lantern",
        "copper",
        "signal",
        "violet");
}

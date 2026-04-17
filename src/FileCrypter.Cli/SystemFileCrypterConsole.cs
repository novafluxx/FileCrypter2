internal sealed class SystemFileCrypterConsole : IFileCrypterConsole
{
    public static SystemFileCrypterConsole Instance { get; } = new();

    private SystemFileCrypterConsole()
    {
    }

    public TextReader In => Console.In;

    public TextWriter Out => Console.Out;

    public TextWriter Error => Console.Error;

    public bool IsInputRedirected => Console.IsInputRedirected;

    public ConsoleKeyInfo ReadKey(bool intercept)
    {
        return Console.ReadKey(intercept);
    }
}

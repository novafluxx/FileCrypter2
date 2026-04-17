internal interface IFileCrypterConsole
{
    TextReader In { get; }

    TextWriter Out { get; }

    TextWriter Error { get; }

    bool IsInputRedirected { get; }

    ConsoleKeyInfo ReadKey(bool intercept);
}

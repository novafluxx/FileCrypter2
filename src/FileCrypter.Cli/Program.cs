internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        return await new FileCrypterCommand(SystemFileCrypterConsole.Instance)
            .RunAsync(args)
            .ConfigureAwait(false);
    }
}

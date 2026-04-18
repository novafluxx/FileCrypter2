namespace FileCrypter.Core;

public sealed class FileCrypterBatchResult
{
    public FileCrypterBatchResult(IReadOnlyList<FileCrypterBatchItemResult> items)
    {
        Items = items;
    }

    public IReadOnlyList<FileCrypterBatchItemResult> Items { get; }

    public int SucceededCount => Items.Count(item => item.Succeeded);

    public int FailedCount => Items.Count - SucceededCount;

    public bool Succeeded => FailedCount == 0;
}

public sealed record FileCrypterBatchItemResult(
    string InputPath,
    string RequestedOutputPath,
    string? OutputPath,
    Exception? Error)
{
    public bool Succeeded => Error is null;
}

namespace FileCrypter.App.Services;

public sealed class BatchTransformResult
{
    public BatchTransformResult(IReadOnlyList<BatchTransformItemResult> items)
    {
        Items = items;
    }

    public IReadOnlyList<BatchTransformItemResult> Items { get; }

    public int SucceededCount => Items.Count(item => item.Succeeded);

    public int FailedCount => Items.Count - SucceededCount;

    public bool Succeeded => FailedCount == 0;
}

public sealed record BatchTransformItemResult(
    string InputPath,
    string RequestedOutputPath,
    string? OutputPath,
    Exception? Error)
{
    public bool Succeeded => Error is null;
}

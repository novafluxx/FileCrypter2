namespace FileCrypter.Desktop.Services;

internal static class WorkflowStatusTextFormatter
{
    private const int MaxStatusDetailLength = 120;

    public static string GetFileSizeText(string path)
    {
        try
        {
            FileInfo fileInfo = new(path);
            if (!fileInfo.Exists)
            {
                return "Size unavailable";
            }

            return FormatByteSize(fileInfo.Length);
        }
        catch
        {
            return "Size unavailable";
        }
    }

    public static string FormatByteSize(long size)
    {
        string[] units = ["B", "KB", "MB", "GB", "TB"];
        double value = size;
        int unitIndex = 0;

        while (value >= 1024 && unitIndex < units.Length - 1)
        {
            value /= 1024;
            unitIndex++;
        }

        return unitIndex == 0
            ? $"{value:0} {units[unitIndex]}"
            : $"{value:0.#} {units[unitIndex]}";
    }

    public static string FormatByteProgress(long inputBytes, long? totalInputBytes)
    {
        if (totalInputBytes is > 0)
        {
            double percent = Math.Clamp(inputBytes * 100d / totalInputBytes.Value, 0, 100);
            return $"{percent:0}% - {FormatByteSize(inputBytes)} / {FormatByteSize(totalInputBytes.Value)}";
        }

        return $"{FormatByteSize(inputBytes)} processed";
    }

    public static string FormatElapsed(TimeSpan elapsed)
    {
        if (elapsed.TotalSeconds < 10)
        {
            return $"{Math.Max(elapsed.TotalSeconds, 0.1):0.0}s";
        }

        if (elapsed.TotalMinutes < 1)
        {
            return $"{Math.Round(elapsed.TotalSeconds):0}s";
        }

        if (elapsed.TotalHours < 1)
        {
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds:00}s";
        }

        return $"{(int)elapsed.TotalHours}h {elapsed.Minutes:00}m";
    }

    public static string SummarizeStatusDetail(string message)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        string singleLineMessage = message
            .Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .FirstOrDefault()
            ?? message.Trim();

        if (singleLineMessage.Length <= MaxStatusDetailLength)
        {
            return singleLineMessage;
        }

        return singleLineMessage[..(MaxStatusDetailLength - 3)].TrimEnd() + "...";
    }
}

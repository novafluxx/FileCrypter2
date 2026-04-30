using CommunityToolkit.Mvvm.Input;
using FileCrypter.Desktop.Services;

namespace FileCrypter.Desktop.ViewModels;

public sealed class FileSelectionPreviewViewModel
{
    private const int MaxDisplayNameLength = 34;

    private const string GenericFileIconPathData = "M6 2 H14 L20 8 V22 H6 Z M14 2 V8 H20";
    private const string TextFileIconPathData = "M6 2 H14 L20 8 V22 H6 Z M14 2 V8 H20 M9 12 H17 M9 15 H17 M9 18 H14";
    private const string EncryptedFileIconPathData = "M6 2 H14 L20 8 V22 H6 Z M14 2 V8 H20 M9 14 V12.5 C9 10.6 10.6 9 12.5 9 C14.4 9 16 10.6 16 12.5 V14 M10 14 H15 V18 H10 Z M12.5 15.2 V16.8";
    private const string ArchiveFileIconPathData = "M4 7 H20 V20 H4 Z M4 10 H20 M8 4 H16 V7 H8 Z M9 13 H15 M9 16 H15";

    private readonly Func<bool>? canRemove;

    public FileSelectionPreviewViewModel(string fullPath, Action? removeAction = null, Func<bool>? canRemove = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fullPath);

        FullPath = Path.GetFullPath(fullPath);
        FileName = Path.GetFileName(FullPath);
        DisplayName = TruncateMiddle(FileName, MaxDisplayNameLength);
        SizeText = GetFileSizeText(FullPath);
        IconPathData = GetIconPathData(FileName);
        this.canRemove = canRemove;

        if (removeAction is not null)
        {
            RemoveCommand = new RelayCommand(removeAction, CanRemove);
        }
    }

    public string FullPath { get; }

    public string FileName { get; }

    public string DisplayName { get; }

    public string SizeText { get; }

    public string IconPathData { get; }

    public RelayCommand? RemoveCommand { get; }

    public void NotifyRemoveCommandChanged()
    {
        RemoveCommand?.NotifyCanExecuteChanged();
    }

    private bool CanRemove()
    {
        return canRemove?.Invoke() ?? true;
    }

    private static string GetFileSizeText(string path)
    {
        return WorkflowStatusTextFormatter.GetFileSizeText(path);
    }

    private static string GetIconPathData(string fileName)
    {
        string normalizedName = fileName.ToLowerInvariant();
        string extension = Path.GetExtension(normalizedName);

        if (normalizedName.EndsWith(".encrypted", StringComparison.Ordinal))
        {
            return EncryptedFileIconPathData;
        }

        return extension switch
        {
            ".zip" or ".tar" or ".gz" or ".tgz" or ".bz2" or ".xz" or ".zst" or ".7z" => ArchiveFileIconPathData,
            ".txt" or ".md" or ".json" or ".xml" or ".yaml" or ".yml" or ".csv" or ".log" or ".cs" or ".js" or ".ts" or ".config" => TextFileIconPathData,
            _ => GenericFileIconPathData,
        };
    }

    private static string TruncateMiddle(string value, int maxLength)
    {
        if (string.IsNullOrEmpty(value) || value.Length <= maxLength)
        {
            return value;
        }

        int leadingLength = Math.Max(1, (maxLength - 3) / 2);
        int trailingLength = Math.Max(1, maxLength - leadingLength - 3);

        return string.Concat(
            value.AsSpan(0, leadingLength),
            "...",
            value.AsSpan(value.Length - trailingLength));
    }
}

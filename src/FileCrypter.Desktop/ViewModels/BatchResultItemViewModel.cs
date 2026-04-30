namespace FileCrypter.Desktop.ViewModels;

public sealed class BatchResultItemViewModel
{
    public BatchResultItemViewModel(
        bool succeeded,
        string statusLabel,
        string statusBrush,
        string titleText,
        string primaryText,
        string detailLabel,
        string detailText,
        string detailBrush,
        string secondaryText)
    {
        Succeeded = succeeded;
        StatusLabel = statusLabel;
        StatusBrush = statusBrush;
        TitleText = titleText;
        PrimaryText = primaryText;
        DetailLabel = detailLabel;
        DetailText = detailText;
        DetailBrush = detailBrush;
        SecondaryText = secondaryText;
    }

    public bool Succeeded { get; }

    public string StatusLabel { get; }

    public string StatusBrush { get; }

    public string TitleText { get; }

    public string PrimaryText { get; }

    public string DetailLabel { get; }

    public string DetailText { get; }

    public string DetailBrush { get; }

    public string SecondaryText { get; }

    public bool HasSecondaryText => !string.IsNullOrWhiteSpace(SecondaryText);
}

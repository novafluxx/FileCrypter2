namespace FileCrypter.Desktop.Services;

public sealed record AppUpdateCheckResult(
    AppUpdateStatus Status,
    string SummaryText,
    string DetailText,
    DateTimeOffset? CheckedAt = null,
    string? AvailableVersion = null);

using FileCrypter.App.Services;
using FileCrypter.App.ViewModels;

namespace FileCrypter.App.Tests.ViewModels;

public sealed class HelpViewModelTests
{
    [Fact]
    public void Constructor_UsesMetadataAndInitialUpdateStatus()
    {
        var viewModel = new HelpViewModel(
            new StubAppMetadataService("v9.8.7", 4),
            new StubAppUpdateService(
                new AppUpdateCheckResult(
                    AppUpdateStatus.NotConfigured,
                    "Update checks are not configured for this build.",
                    "This build is waiting on a future release channel.")));

        Assert.Equal("Help and recovery", viewModel.Title);
        Assert.Equal("v9.8.7", viewModel.CurrentVersion);
        Assert.Equal("Encrypted file format v4", viewModel.FormatVersionText);
        Assert.Contains("local-first", viewModel.AboutSummaryText, StringComparison.Ordinal);
        Assert.Contains("record it before sealing the file", viewModel.RecoveryWarningText, StringComparison.Ordinal);
        Assert.Contains("Fluent UI System Icons", viewModel.ThirdPartyNoticeSummary, StringComparison.Ordinal);
        Assert.Contains("Fluent UI System Icons", viewModel.ThirdPartyNoticeText, StringComparison.Ordinal);
        Assert.Contains("Copyright (c) Microsoft Corporation", viewModel.ThirdPartyNoticeText, StringComparison.Ordinal);
        Assert.Contains("MIT License", viewModel.ThirdPartyNoticeText, StringComparison.Ordinal);
        Assert.Equal("Update checks are not configured for this build.", viewModel.UpdateSummaryText);
        Assert.Equal("Help and recovery", viewModel.StatusText);
        Assert.Equal("Update checks are not configured for this build.", viewModel.ProgressText);
        Assert.Equal(7, viewModel.WorkflowTopics.Count);
        Assert.Equal(4, viewModel.TroubleshootingTopics.Count);
        Assert.Equal(3, viewModel.SafetyTopics.Count);
    }

    [Fact]
    public void Constructor_IncludesPasswordGeneratorRecoveryGuidance()
    {
        var viewModel = new HelpViewModel(
            new StubAppMetadataService("v0.1.0", 1),
            new StubAppUpdateService(
                new AppUpdateCheckResult(
                    AppUpdateStatus.NotConfigured,
                    "Waiting for an update check.",
                    "No update check has run yet.")));

        HelpTopicViewModel generatorTopic = Assert.Single(
            viewModel.WorkflowTopics,
            topic => topic.Title == "Password generator");
        Assert.Contains("random password", generatorTopic.Description, StringComparison.Ordinal);
        Assert.Contains("maximum entropy", generatorTopic.Description, StringComparison.Ordinal);
        Assert.Contains("memorable passphrase", generatorTopic.Description, StringComparison.Ordinal);
        Assert.Contains("record them before encrypting", generatorTopic.Description, StringComparison.Ordinal);

        HelpTopicViewModel recoveryTopic = Assert.Single(
            viewModel.SafetyTopics,
            topic => topic.Title == "Recovery limits");
        Assert.Contains("Generated passwords and passphrases are not saved", recoveryTopic.Description, StringComparison.Ordinal);
        Assert.Contains("unless you record them yourself", recoveryTopic.Description, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CheckForUpdatesCommand_RefreshesUpdateStateAndTimestamp()
    {
        var viewModel = new HelpViewModel(
            new StubAppMetadataService("v0.1.0", 1),
            new StubAppUpdateService(
                new AppUpdateCheckResult(
                    AppUpdateStatus.NotConfigured,
                    "Waiting for an update check.",
                    "No update check has run yet."),
                new AppUpdateCheckResult(
                    AppUpdateStatus.UpToDate,
                    "You already have the latest FileCrypter build.",
                    "No newer build is available on the configured channel.",
                    new DateTimeOffset(2026, 4, 18, 9, 30, 0, TimeSpan.Zero))));

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("You already have the latest FileCrypter build.", viewModel.UpdateSummaryText);
        Assert.Equal("No newer build is available on the configured channel.", viewModel.UpdateDetailText);
        Assert.Contains("Last checked:", viewModel.LastCheckedText, StringComparison.Ordinal);
        Assert.True(viewModel.HasLastCheckedText);
        Assert.Equal("Help and recovery", viewModel.StatusText);
        Assert.Equal("You already have the latest FileCrypter build.", viewModel.ProgressText);
    }

    [Fact]
    public async Task CheckForUpdatesCommand_WhenServiceFails_ShowsFriendlyFailure()
    {
        var viewModel = new HelpViewModel(
            new StubAppMetadataService("v0.1.0", 1),
            new ThrowingAppUpdateService());

        await viewModel.CheckForUpdatesCommand.ExecuteAsync(null);

        Assert.Equal("FileCrypter could not check for updates.", viewModel.UpdateSummaryText);
        Assert.Contains("Network unavailable.", viewModel.UpdateDetailText, StringComparison.Ordinal);
        Assert.True(viewModel.HasLastCheckedText);
    }

    private sealed class StubAppMetadataService(string displayVersion, ushort formatVersion) : IAppMetadataService
    {
        public string DisplayVersion { get; } = displayVersion;

        public ushort FormatVersion { get; } = formatVersion;
    }

    private sealed class StubAppUpdateService(
        AppUpdateCheckResult currentStatus,
        AppUpdateCheckResult? checkedStatus = null) : IAppUpdateService
    {
        public AppUpdateCheckResult GetCurrentStatus()
        {
            return currentStatus;
        }

        public Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(checkedStatus ?? currentStatus);
        }
    }

    private sealed class ThrowingAppUpdateService : IAppUpdateService
    {
        public AppUpdateCheckResult GetCurrentStatus()
        {
            return new AppUpdateCheckResult(
                AppUpdateStatus.NotConfigured,
                "Waiting for an update check.",
                "No update check has run yet.");
        }

        public Task<AppUpdateCheckResult> CheckForUpdatesAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromException<AppUpdateCheckResult>(new InvalidOperationException("Network unavailable."));
        }
    }
}
